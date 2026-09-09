# MultiTenancy — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** Multi-Tenancy module. It provides tenant lifecycle management, member and invitation management, configurable tenant resolution strategies (header, subdomain, claim, route segment, composite), automatic EF Core global query filtering, and SaveChanges interception for `ITenantScoped` data isolation.

---

## Module Anatomy

```
Modules/
├── MultiTenancy/                                     # Module implementation
│   ├── Configuration/
│   │   └── TenantSettings.cs                        # DynamicSettingsSection: resolution, limits, invitations
│   ├── Controllers/
│   │   ├── TenantsController.cs                     # Tenant CRUD, suspend/reactivate
│   │   ├── MembersController.cs                     # Member list, add, remove, update role
│   │   └── InvitationsController.cs                 # Invite, accept, revoke, list invitations
│   ├── Entities/
│   │   ├── Tenant.cs                                # Tenant entity (name, slug, status, plan, metadata)
│   │   ├── TenantMember.cs                          # User ↔ Tenant relationship (role, joined date)
│   │   └── TenantInvitation.cs                      # Pending invitation (email, role, token, expiry)
│   ├── Features/
│   │   ├── Tenants/                                 # CreateTenant, GetTenants, GetMyTenants, GetTenant, UpdateTenant, SuspendTenant, ReactivateTenant, DeleteTenant
│   │   ├── Members/                                 # GetMembers, AddMember, RemoveMember, UpdateMemberRole, GetCurrentMembership
│   │   ├── Invitations/                             # CreateInvitation, GetInvitations, RevokeInvitation, AcceptInvitation
│   │   ├── GetTenantSummaryHandler.cs               # Cross-module handler for GetTenantSummary
│   │   └── ValidateTenantAccessHandler.cs           # Cross-module handler for ValidateTenantAccess
│   ├── Middleware/
│   │   ├── TenantResolutionMiddleware.cs            # Resolves current tenant per request
│   │   └── TenantOptionalAttribute.cs               # Marks endpoints bypassing RequireTenant
│   ├── Persistence/
│   │   ├── TenantModuleDbContext.cs                 # DbContext with "tenancy" schema
│   │   ├── TenantModelBuilderExtensions.cs          # Global query filter extension for ITenantScoped
│   │   └── TenantSaveChangesInterceptor.cs          # Stamping & validation interceptor for ITenantScoped
│   ├── Resolution/
│   │   ├── ITenantResolver.cs                       # Abstraction for tenant resolution strategies
│   │   ├── HeaderTenantResolver.cs                  # Resolves from X-Tenant-Id or X-Tenant-Slug header
│   │   ├── SubdomainTenantResolver.cs               # Resolves from subdomain (acme.api.com)
│   │   ├── ClaimTenantResolver.cs                   # Resolves from authenticated user's TenantId claim
│   │   ├── RouteSegmentTenantResolver.cs            # Resolves from URL path segment (/api/v1/{tenantSlug}/...)
│   │   └── CompositeTenantResolver.cs               # Chains multiple resolvers with priority order
│   ├── Services/
│   │   ├── ITenantService.cs                        # Tenant lifecycle operations
│   │   ├── TenantService.cs                         # Implementation
│   │   ├── ITenantContext.cs                        # Scoped current-tenant accessor
│   │   └── TenantContext.cs                         # Implementation (set by middleware)
│   ├── Workers/
│   │   └── InvitationCleanupWorker.cs               # Periodic: purge expired invitations
│   ├── MultiTenancyModule.cs                        # IModule entry point
│   ├── appsettings.MultiTenancy.json                # Module settings
│   └── appsettings.MultiTenancy.Development.json    # Dev overlay
│
└── MultiTenancy.Contracts/                           # Companion contracts
    ├── Constants/
    │   ├── TenantRoles.cs                           # Owner, Admin, Member, ReadOnly
    │   ├── TenantPolicies.cs                        # TenantOwner, TenantAdmin, TenantMember
    │   ├── TenantClaimTypes.cs                      # TenantId, TenantSlug, TenantRole
    │   └── TenantStatus.cs                          # Active, Suspended, Deleted
    ├── Dtos/
    │   ├── TenantDto.cs                             # Summary DTO
    │   ├── TenantDetailDto.cs                       # Full DTO with member count, plan, metadata
    │   ├── TenantMemberDto.cs                       # Member summary
    │   └── TenantInvitationDto.cs                   # Invitation summary
    ├── Entities/
    │   └── ITenantScoped.cs                         # Marker interface for tenant-isolated data
    ├── Events/
    │   ├── TenantCreatedEvent.cs                    # (TenantId, Name, Slug, OwnerUserId)
    │   ├── TenantSuspendedEvent.cs                  # (TenantId, Reason, SuspendedBy)
    │   ├── TenantReactivatedEvent.cs                # (TenantId, ReactivatedBy)
    │   ├── TenantDeletedEvent.cs                    # (TenantId, DeletedBy)
    │   ├── MemberAddedEvent.cs                      # (TenantId, UserId, Role)
    │   ├── MemberRemovedEvent.cs                    # (TenantId, UserId, RemovedBy)
    │   ├── MemberRoleChangedEvent.cs                # (TenantId, UserId, OldRole, NewRole)
    │   ├── InvitationCreatedEvent.cs                # (TenantId, Email, Role, InvitedBy)
    │   └── InvitationAcceptedEvent.cs               # (TenantId, UserId, InvitationId)
    ├── Requests/
    │   ├── GetTenantSummary.cs                      # IRequest<TenantDto> — query by tenant ID
    │   └── ValidateTenantAccess.cs                  # IRequest<bool> — check user has role in tenant
    └── MultiTenancy.Contracts.csproj
```
