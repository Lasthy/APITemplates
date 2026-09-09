using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Entities;
using MultiTenancy.Services;

namespace MultiTenancy.Persistence;

/// <summary>
/// Extension methods for configuring EF Core model builders with multi-tenant query filters.
/// </summary>
public static class TenantModelBuilderExtensions
{
    /// <summary>
    /// Applies global query filters to all entity types in the model implementing <see cref="ITenantScoped"/>,
    /// binding them to the DbContext's tenant context accessor for parameterized SQL query generation.
    /// </summary>
    /// <param name="builder">The EF Core model builder.</param>
    /// <param name="context">The DbContext instance containing the tenant context field/property.</param>
    /// <param name="tenantContext">Optional explicit tenant context accessor.</param>
    public static void ApplyTenantFilters(this ModelBuilder builder, DbContext context, ITenantContext? tenantContext = null)
    {
        var contextType = context.GetType();
        var contextExpr = Expression.Constant(context, contextType);

        // Find field or property of type ITenantContext on context
        MemberExpression? tenantContextMemberExpr = null;
        var field = contextType
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(f => typeof(ITenantContext).IsAssignableFrom(f.FieldType));

        if (field != null)
        {
            tenantContextMemberExpr = Expression.Field(contextExpr, field);
        }
        else
        {
            var prop = contextType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(p => typeof(ITenantContext).IsAssignableFrom(p.PropertyType));
            if (prop != null)
            {
                tenantContextMemberExpr = Expression.Property(contextExpr, prop);
            }
        }

        Expression isBypassedExpr;
        Expression hasValueExpr;
        Expression valueExpr;

        if (tenantContextMemberExpr != null)
        {
            // context._tenantContext.IsFilterBypassed
            isBypassedExpr = Expression.Property(tenantContextMemberExpr, nameof(ITenantContext.IsFilterBypassed));

            // context._tenantContext.TenantId
            var tenantIdExpr = Expression.Property(tenantContextMemberExpr, nameof(ITenantContext.TenantId));
            hasValueExpr = Expression.Property(tenantIdExpr, nameof(Nullable<DefaultId>.HasValue));
            valueExpr = Expression.Property(tenantIdExpr, nameof(Nullable<DefaultId>.Value));
        }
        else if (tenantContext != null)
        {
            var tcExpr = Expression.Constant(tenantContext, typeof(ITenantContext));
            isBypassedExpr = Expression.Property(tcExpr, nameof(ITenantContext.IsFilterBypassed));
            var tenantIdExpr = Expression.Property(tcExpr, nameof(ITenantContext.TenantId));
            hasValueExpr = Expression.Property(tenantIdExpr, nameof(Nullable<DefaultId>.HasValue));
            valueExpr = Expression.Property(tenantIdExpr, nameof(Nullable<DefaultId>.Value));
        }
        else
        {
            return;
        }

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var tenantIdProp = Expression.Property(parameter, nameof(ITenantScoped.TenantId));

                // e.TenantId == tenantContext.TenantId.Value
                var equalsExpr = Expression.Equal(tenantIdProp, valueExpr);

                // tenantContext.TenantId.HasValue && e.TenantId == tenantContext.TenantId.Value
                var tenantMatchExpr = Expression.AndAlso(hasValueExpr, equalsExpr);

                // tenantContext.IsFilterBypassed || (tenantContext.TenantId.HasValue && ...)
                var filterBody = Expression.OrElse(isBypassedExpr, tenantMatchExpr);

                var lambda = Expression.Lambda(filterBody, parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    /// <summary>
    /// Applies global query filters to all entity types in the model implementing <see cref="ITenantScoped"/>.
    /// </summary>
    /// <param name="builder">The EF Core model builder.</param>
    /// <param name="tenantContext">The tenant context accessor.</param>
    public static void ApplyTenantFilters(this ModelBuilder builder, ITenantContext tenantContext)
    {
        var tcExpr = Expression.Constant(tenantContext, typeof(ITenantContext));
        var isBypassedExpr = Expression.Property(tcExpr, nameof(ITenantContext.IsFilterBypassed));
        var tenantIdExpr = Expression.Property(tcExpr, nameof(ITenantContext.TenantId));
        var hasValueExpr = Expression.Property(tenantIdExpr, nameof(Nullable<DefaultId>.HasValue));
        var valueExpr = Expression.Property(tenantIdExpr, nameof(Nullable<DefaultId>.Value));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var tenantIdProp = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var equalsExpr = Expression.Equal(tenantIdProp, valueExpr);
                var tenantMatchExpr = Expression.AndAlso(hasValueExpr, equalsExpr);
                var filterBody = Expression.OrElse(isBypassedExpr, tenantMatchExpr);

                var lambda = Expression.Lambda(filterBody, parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
