# CMS — Module Agent Guide

This project is a self-contained **Modular Web API (MWA)** Content Management System module. It provides a lightweight, headless content management system for MWA applications: structured content with code-defined content types, revision history with rollback, automatic slug generation, tag-based organization, media attachments via Kernel `IFileStorage`, and a read-optimized public API.

---

## Module Anatomy

```
Modules/
├── Cms/                                              # Module implementation
│   ├── Configuration/
│   │   └── CmsSettings.cs                           # DynamicSettingsSection: defaults, limits, media
│   ├── Controllers/
│   │   ├── ContentController.cs                     # Content CRUD, publish/unpublish/archive, revisions
│   │   ├── ContentPublicController.cs               # Public read-only endpoints (no auth required)
│   │   ├── ContentTypesController.cs                # List/detail content types (read-only, code-defined)
│   │   ├── TagsController.cs                        # Tag CRUD
│   │   └── MediaController.cs                       # Media upload, list, delete
│   ├── Entities/
│   │   ├── ContentItem.cs                           # Core content entity (title, slug, body, type, status)
│   │   ├── ContentRevision.cs                       # Point-in-time snapshot of a ContentItem
│   │   ├── ContentTag.cs                            # Tag entity (name, slug, description)
│   │   ├── ContentItemTag.cs                        # Many-to-many join table
│   │   └── ContentMedia.cs                          # Media attachment linked to content item
│   ├── Features/
│   │   ├── Content/                                 # CreateContent, GetContents, GetContent, UpdateContent, DeleteContent, PublishContent, UnpublishContent, ArchiveContent, RestoreRevision, SetContentTags
│   │   ├── ContentTypes/                            # GetContentTypes, GetContentType
│   │   ├── Tags/                                    # CreateTag, GetTags, GetTag, UpdateTag, DeleteTag
│   │   ├── Media/                                   # UploadMedia, GetMedia, GetMediaItem, DeleteMedia, GetContentMedia
│   │   ├── Revisions/                               # GetRevisions, GetRevision, CompareRevisions
│   │   ├── Public/                                  # GetPublishedContents, GetPublishedContentBySlug, GetPublicTags, GetPublicTagBySlug
│   │   ├── GetContentBySlugHandler.cs               # Cross-module handler for GetContentBySlug
│   │   └── GetContentSummaryHandler.cs              # Cross-module handler for GetContentSummary
│   ├── ContentTypes/
│   │   ├── IContentType.cs                          # Content type contract (static abstract interface)
│   │   ├── ContentTypeRegistry.cs                   # Singleton registry + ContentTypeDescriptor
│   │   ├── ContentField.cs                          # Field definition (name, type, required, validation)
│   │   └── BuiltIn/
│   │       ├── PageContentType.cs                   # Generic page with title + body
│   │       ├── PostContentType.cs                   # Blog post with title + body + excerpt + featured image
│   │       └── FaqContentType.cs                    # FAQ with question + answer pairs
│   ├── Persistence/
│   │   └── CmsModuleDbContext.cs                    # DbContext with "cms" schema
│   ├── Services/
│   │   ├── IContentService.cs                       # Content lifecycle operations
│   │   ├── ContentService.cs                        # Implementation
│   │   ├── ISlugService.cs                          # Slug generation and uniqueness
│   │   ├── SlugService.cs                           # Implementation
│   │   ├── IContentMediaService.cs                  # Media attachment operations (wraps Kernel IFileStorage)
│   │   └── ContentMediaService.cs                   # Implementation
│   ├── Workers/
│   │   ├── MediaCleanupWorker.cs                    # Purges orphaned media files past grace period
│   │   └── ScheduledPublishingWorker.cs             # Auto-publishes scheduled content when enabled
│   ├── CmsModule.cs                                 # IModule entry point
│   ├── appsettings.Cms.json                         # Module settings
│   └── appsettings.Cms.Development.json             # Dev overlay
│
└── Cms.Contracts/                                    # Companion contracts
    ├── Constants/
    │   ├── CmsPolicies.cs                           # ManageContent, PublishContent
    │   ├── ContentStatus.cs                         # Draft, Published, Archived
    │   └── ContentFieldType.cs                      # String, Text, Html, Markdown, Integer, Decimal, Boolean, DateTime, Url, Image, Json
    ├── Dtos/
    │   ├── ContentItemDto.cs                        # Summary DTO
    │   ├── ContentItemDetailDto.cs                  # Full DTO with body, metadata, tags, media
    │   ├── ContentRevisionDto.cs                    # Revision summary
    │   ├── ContentRevisionDetailDto.cs              # Full revision snapshot
    │   ├── ContentTypeDto.cs                        # Content type summary
    │   ├── ContentTypeDetailDto.cs                  # Content type with field definitions
    │   ├── ContentFieldDto.cs                       # Field definition DTO
    │   ├── ContentTagDto.cs                         # Tag DTO
    │   ├── ContentMediaDto.cs                       # Media attachment DTO
    │   ├── RevisionComparisonDto.cs                 # Diff between two revisions
    │   ├── PublicTagDto.cs                          # Public tag summary with published count
    │   └── PublicTagDetailDto.cs                    # Public tag detail with published content
    ├── Events/
    │   ├── ContentCreatedEvent.cs                   # (ContentId, ContentType, Title, Slug, AuthorId)
    │   ├── ContentPublishedEvent.cs                 # (ContentId, ContentType, Slug, PublishedBy)
    │   ├── ContentUnpublishedEvent.cs               # (ContentId, ContentType, Slug, UnpublishedBy)
    │   ├── ContentArchivedEvent.cs                  # (ContentId, ContentType, Slug, ArchivedBy)
    │   ├── ContentUpdatedEvent.cs                   # (ContentId, ContentType, RevisionNumber)
    │   ├── ContentDeletedEvent.cs                   # (ContentId, ContentType, Slug, DeletedBy)
    │   └── MediaUploadedEvent.cs                    # (MediaId, ContentId, FileName, MimeType, SizeBytes)
    ├── Requests/
    │   ├── GetContentBySlug.cs                      # IRequest<ContentItemDetailDto>
    │   └── GetContentSummary.cs                     # IRequest<ContentItemDto>
    └── Cms.Contracts.csproj
```

---

## Defining Custom Content Types

Other modules can define custom content types by implementing `IContentType`:

```csharp
public class DocPageContentType : IContentType
{
    public static string Name => "DocPage";
    public static string Description => "Documentation page with sidebar navigation.";
    public static string Icon => "book";
    public static bool HasBody => true;
    public static string BodyFormat => "markdown";
    public static IReadOnlyList<ContentField> Fields =>
    [
        new("Category", "Category", ContentFieldType.String, IsRequired: true, MaxLength: 100),
        new("ParentSlug", "Parent Page Slug", ContentFieldType.String, Description: "Slug of the parent page for hierarchy"),
        new("NavigationOrder", "Nav Order", ContentFieldType.Integer, DefaultValue: "0")
    ];
    public static bool SupportsTags => true;
    public static bool SupportsMedia => true;
    public static int? MaxRevisions => null;
}
```

Registering custom types from another module's assembly:

```csharp
// In your module's Register method
var registry = services.BuildServiceProvider().GetService<IContentTypeRegistry>();
// or inject/resolve and call:
registry?.RegisterFromAssembly(typeof(MyModule).Assembly);
```

---

## Querying Content Across Modules

Other modules should reference `Cms.Contracts` only and use `IDispatcher`:

```csharp
var content = await dispatcher.DispatchAsync(new GetContentBySlug("Page", "terms-of-service"), ct);
if (content.IsSuccess)
{
    Console.WriteLine(content.Value.Title);
}
```
