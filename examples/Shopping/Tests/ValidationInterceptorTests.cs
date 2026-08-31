using System.Reflection;
using Kernel.Authorization;
using Kernel.Authorization.Extensions;
using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Kernel.Validation;
using Kernel.Validation.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Shopping.Tests;

public class ValidationInterceptorTests
{
    #region Test Models, Validators, and Handlers

    public record CreateCustomerRequest(string Name, string Email, int Age) : IRequest<string>;

    public class CreateCustomerValidator : IValidator<CreateCustomerRequest>
    {
        public ValidationResult Validate(CreateCustomerRequest request)
        {
            var errors = new List<ValidationError>();
            if (string.IsNullOrWhiteSpace(request.Name))
                errors.Add(new ValidationError("Name", "Name is required."));
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
                errors.Add(new ValidationError("Email", "Invalid email format."));
            if (request.Age < 18)
                errors.Add(new ValidationError("Age", "Must be at least 18 years old."));

            return errors.Count > 0 ? ValidationResult.Failure(errors) : ValidationResult.Success();
        }
    }

    public class CreateCustomerSecondaryValidator : IValidator<CreateCustomerRequest>
    {
        public ValidationResult Validate(CreateCustomerRequest request)
        {
            if (request.Name != null && request.Name.Contains("Blocked"))
            {
                return ValidationResult.Failure("Name", "Name is on the blocked list.");
            }
            return ValidationResult.Success();
        }
    }

    [Validate]
    public class ValidatedCustomerHandler : IRequestHandler<CreateCustomerRequest, string>
    {
        public Task<Result<string>> HandleAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"Customer created: {request.Name}"));
        }
    }

    public class UnvalidatedCustomerHandler : IRequestHandler<CreateCustomerRequest, string>
    {
        public Task<Result<string>> HandleAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"Unvalidated customer created: {request.Name}"));
        }
    }

    public record NoValidatorRequest(string Value) : IRequest<string>;

    [Validate]
    public class NoValidatorHandler : IRequestHandler<NoValidatorRequest, string>
    {
        public Task<Result<string>> HandleAsync(NoValidatorRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>($"Processed: {request.Value}"));
        }
    }

    public record OrderExecutionRequest(string Value) : IRequest<string>;

    public class TrackingOrderExecutionValidator : IValidator<OrderExecutionRequest>
    {
        public static bool WasExecuted { get; set; }

        public ValidationResult Validate(OrderExecutionRequest request)
        {
            WasExecuted = true;
            if (request.Value == "invalid")
                return ValidationResult.Failure("Value", "Value cannot be invalid.");
            return ValidationResult.Success();
        }
    }

    [Authorize("Authenticated")]
    [Validate]
    public class OrderExecutionHandler : IRequestHandler<OrderExecutionRequest, string>
    {
        public static bool HandlerWasExecuted { get; set; }

        public Task<Result<string>> HandleAsync(OrderExecutionRequest request, CancellationToken cancellationToken = default)
        {
            HandlerWasExecuted = true;
            return Task.FromResult(Result.Success<string>($"Success: {request.Value}"));
        }
    }

    public record ThrowingValidationRequest(string Value) : IRequest<string>;

    public class ThrowingValidator : IValidator<ThrowingValidationRequest>
    {
        public ValidationResult Validate(ThrowingValidationRequest request)
        {
            throw new InvalidOperationException("Validator crashed!");
        }
    }

    [Validate]
    public class ThrowingValidationHandler : IRequestHandler<ThrowingValidationRequest, string>
    {
        public Task<Result<string>> HandleAsync(ThrowingValidationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success<string>("Should not reach here"));
        }
    }

    private class TestScopeContext : IScopeContext
    {
        public DefaultId? UserId { get; set; }
        public bool IsAuthenticated { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
        public bool IsSystem => IsInRole(Kernel.Constants.Roles.System);
        public string? CorrelationId { get; set; }
        public string? GetClaim(string claimType) => Claims.TryGetValue(claimType, out var v) ? v : null;
        public IReadOnlyList<string> GetClaimValues(string claimType) => Claims.TryGetValue(claimType, out var v) ? [v] : [];
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    [Fact]
    public void ValidationResult_Success_ShouldBeValidAndHaveNoErrors()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_SingleError_ShouldContainError()
    {
        var result = ValidationResult.Failure("Email", "Email is required.");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Email", result.Errors[0].Field);
        Assert.Equal("Email is required.", result.Errors[0].Message);
    }

    [Fact]
    public void ValidationResult_MultipleErrors_ShouldContainAllErrors()
    {
        var errors = new[]
        {
            new ValidationError("Name", "Name is required."),
            new ValidationError("Age", "Must be 18.")
        };

        var result = ValidationResult.Failure(errors);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("Name", result.Errors[0].Field);
        Assert.Equal("Age", result.Errors[1].Field);
    }

    [Fact]
    public void ValidationException_ShouldPopulateErrorsAndFormatMessage()
    {
        var result = ValidationResult.Failure(
            new ValidationError("Email", "Invalid email."),
            new ValidationError("Password", "Too short.")
        );

        var exception = new ValidationException(result);

        Assert.Equal(2, exception.Errors.Count);
        Assert.Equal(result, exception.Result);
        Assert.Contains("Email: Invalid email.", exception.Message);
        Assert.Contains("Password: Too short.", exception.Message);
    }

    [Fact]
    public void ValidationException_SingleFieldConstructor_ShouldFormatMessage()
    {
        var exception = new ValidationException("Username", "Cannot be empty");

        Assert.Single(exception.Errors);
        Assert.Equal("Username", exception.Errors[0].Field);
        Assert.Equal("Cannot be empty", exception.Errors[0].Message);
        Assert.Contains("Username: Cannot be empty", exception.Message);
    }

    [Fact]
    public void ValidationException_GeneralMessageConstructor_ShouldHaveEmptyErrors()
    {
        var exception = new ValidationException("General failure");

        Assert.Empty(exception.Errors);
        Assert.True(exception.Result.IsValid);
        Assert.Equal("General failure", exception.Message);
    }

    [Fact]
    public async Task Dispatcher_WithValidRequest_ShouldPassValidationAndExecuteHandler()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerValidator>();
        services.AddScoped<IRequestHandler<CreateCustomerRequest, string>, ValidatedCustomerHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var request = new CreateCustomerRequest("John Doe", "john@example.com", 25);
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Customer created: John Doe", result.Value);
    }

    [Fact]
    public async Task Dispatcher_WithInvalidRequest_ShouldShortCircuitWithValidationFailure()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerValidator>();
        services.AddScoped<IRequestHandler<CreateCustomerRequest, string>, ValidatedCustomerHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var request = new CreateCustomerRequest("", "invalid-email", 16);
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.IsType<ValidationException>(result.Error);

        var valEx = (ValidationException)result.Error;
        Assert.Equal(3, valEx.Errors.Count);
        Assert.Contains(valEx.Errors, e => e.Field == "Name" && e.Message == "Name is required.");
        Assert.Contains(valEx.Errors, e => e.Field == "Email" && e.Message == "Invalid email format.");
        Assert.Contains(valEx.Errors, e => e.Field == "Age" && e.Message == "Must be at least 18 years old.");

        Assert.Equal(3, result.Messages.Length);
        Assert.Contains("Name: Name is required.", result.Messages);
        Assert.Contains("Email: Invalid email format.", result.Messages);
        Assert.Contains("Age: Must be at least 18 years old.", result.Messages);
    }

    [Fact]
    public async Task Dispatcher_WithMultipleValidators_ShouldCombineAllValidationErrors()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerValidator>();
        services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerSecondaryValidator>();
        services.AddScoped<IRequestHandler<CreateCustomerRequest, string>, ValidatedCustomerHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Fails primary validator (Age < 18) AND secondary validator (Name contains "Blocked")
        var request = new CreateCustomerRequest("Blocked Customer", "valid@example.com", 15);
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsFailure);
        var valEx = Assert.IsType<ValidationException>(result.Error);
        Assert.Equal(2, valEx.Errors.Count);
        Assert.Contains(valEx.Errors, e => e.Field == "Age");
        Assert.Contains(valEx.Errors, e => e.Field == "Name" && e.Message == "Name is on the blocked list.");
    }

    [Fact]
    public async Task Dispatcher_WithoutValidateAttribute_ShouldNotRunValidator()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerValidator>();
        services.AddScoped<IRequestHandler<CreateCustomerRequest, string>, UnvalidatedCustomerHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Has invalid data, but handler lacks [Validate] attribute
        var request = new CreateCustomerRequest("", "invalid-email", 10);
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Unvalidated customer created: ", result.Value);
    }

    [Fact]
    public async Task Dispatcher_WithValidateAttribute_ButNoValidatorRegistered_ShouldProceed()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddScoped<IRequestHandler<NoValidatorRequest, string>, NoValidatorHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var request = new NoValidatorRequest("Hello");
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Processed: Hello", result.Value);
    }

    [Fact]
    public async Task Pipeline_AuthorizationFailsBeforeValidationRuns()
    {
        TrackingOrderExecutionValidator.WasExecuted = false;
        OrderExecutionHandler.HandlerWasExecuted = false;

        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddAuthorizationPolicies();

        var scopeContext = new TestScopeContext { IsAuthenticated = false };
        services.AddSingleton<IScopeContext>(scopeContext);

        services.AddScoped<IValidator<OrderExecutionRequest>, TrackingOrderExecutionValidator>();
        services.AddScoped<IRequestHandler<OrderExecutionRequest, string>, OrderExecutionHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var request = new OrderExecutionRequest("invalid");
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedAccessException>(result.Error);
        // Authorization (Order = -100) failed first, so Validation (Order = -50) was never called!
        Assert.False(TrackingOrderExecutionValidator.WasExecuted);
        Assert.False(OrderExecutionHandler.HandlerWasExecuted);
    }

    [Fact]
    public async Task Pipeline_ValidationFailsBeforeHandlerExecutes()
    {
        TrackingOrderExecutionValidator.WasExecuted = false;
        OrderExecutionHandler.HandlerWasExecuted = false;

        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddAuthorizationPolicies();

        var scopeContext = new TestScopeContext
        {
            IsAuthenticated = true,
            Roles = ["Admin"]
        };
        services.AddSingleton<IScopeContext>(scopeContext);

        services.AddScoped<IValidator<OrderExecutionRequest>, TrackingOrderExecutionValidator>();
        services.AddScoped<IRequestHandler<OrderExecutionRequest, string>, OrderExecutionHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var request = new OrderExecutionRequest("invalid");
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationException>(result.Error);
        // Validation ran and failed, so handler was never executed!
        Assert.True(TrackingOrderExecutionValidator.WasExecuted);
        Assert.False(OrderExecutionHandler.HandlerWasExecuted);
    }

    [Fact]
    public async Task ValidationInterceptor_WhenValidatorThrows_ShouldReturnFailureResult()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddTransient<ValidationInterceptor>();
        services.AddScoped<IValidator<ThrowingValidationRequest>, ThrowingValidator>();
        services.AddScoped<IRequestHandler<ThrowingValidationRequest, string>, ThrowingValidationHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var request = new ThrowingValidationRequest("test");
        var result = await dispatcher.DispatchAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Validator crashed!", result.Error?.Message);
    }

    [Fact]
    public void AutoDiscovery_RegisterValidatorsFromAssembly_ShouldRegisterAllValidatorsInAssembly()
    {
        var services = new ServiceCollection();
        services.RegisterValidatorsFromAssembly(typeof(ValidationInterceptorTests).Assembly);

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var customerValidators = scope.ServiceProvider.GetServices<IValidator<CreateCustomerRequest>>().ToList();
        Assert.NotEmpty(customerValidators);
        Assert.Contains(customerValidators, v => v is CreateCustomerValidator);
        Assert.Contains(customerValidators, v => v is CreateCustomerSecondaryValidator);

        var orderValidator = scope.ServiceProvider.GetService<IValidator<OrderExecutionRequest>>();
        Assert.NotNull(orderValidator);
        Assert.IsType<TrackingOrderExecutionValidator>(orderValidator);
    }
}
