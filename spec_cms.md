# CMS Module — Specification

**Template:** `mwa-module-cms` · **Short Name:** `modular-webapi-module-cms` · **Type:** Project

This module provides a lightweight, headless content management system for MWA applications: structured content with code-defined content types, revision history with rollback, automatic slug generation, tag-based organization, media attachments via Kernel `IFileStorage`, and a read-optimized public API — all without the weight of a full CMS platform.

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
│   │   ├── Content/                                 # CreateContent, GetContents, GetContent, UpdateContent, DeleteContent, PublishContent, UnpublishContent, ArchiveContent, RestoreRevision
│   │   ├── ContentTypes/                            # GetContentTypes, GetContentType
│   │   ├── Tags/                                    # CreateTag, GetTags, GetTag, UpdateTag, DeleteTag
│   │   ├── Media/                                   # UploadMedia, GetMedia, DeleteMedia, GetContentMedia
│   │   ├── Revisions/                               # GetRevisions, GetRevision, CompareRevisions
│   │   └── GetContentBySlugHandler.cs               # Cross-module handler for GetContentBySlug
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
│   │   └── MediaCleanupWorker.cs                    # Purges orphaned media files not linked to any content
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
    │   ├── ContentItemDto.cs                        # Summary DTO (for lists)
    │   ├── ContentItemDetailDto.cs                  # Full DTO with body, metadata, tags, media
    │   ├── ContentRevisionDto.cs                    # Revision summary
    │   ├── ContentRevisionDetailDto.cs              # Full revision snapshot
    │   ├── ContentTypeDto.cs                        # Content type summary
    │   ├── ContentTypeDetailDto.cs                  # Content type with field definitions
    │   ├── ContentFieldDto.cs                       # Field definition DTO
    │   ├── ContentTagDto.cs                         # Tag DTO
    │   ├── ContentMediaDto.cs                       # Media attachment DTO
    │   └── RevisionComparisonDto.cs                 # Diff between two revisions
    ├── Events/
    │   ├── ContentCreatedEvent.cs                   # (ContentId, ContentType, Title, Slug, AuthorId)
    │   ├── ContentPublishedEvent.cs                 # (ContentId, ContentType, Slug, PublishedBy)
    │   ├── ContentUnpublishedEvent.cs               # (ContentId, ContentType, Slug, UnpublishedBy)
    │   ├── ContentArchivedEvent.cs                  # (ContentId, ContentType, Slug, ArchivedBy)
    │   ├── ContentUpdatedEvent.cs                   # (ContentId, ContentType, RevisionNumber)
    │   ├── ContentDeletedEvent.cs                   # (ContentId, ContentType, Slug, DeletedBy)
    │   └── MediaUploadedEvent.cs                    # (MediaId, ContentId, FileName, MimeType, SizeBytes)
    ├── Requests/
    │   ├── GetContentBySlug.cs                      # IRequest<ContentItemDetailDto> — query by slug
    │   └── GetContentSummary.cs                     # IRequest<ContentItemDto> — query by ID
    └── Cms.Contracts.csproj
```

---

## 1. Entities

### `ContentItem`

```csharp
public class ContentItem : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Content type name (e.g., "Page", "Post", "Faq").
    /// Maps to a registered IContentType implementation.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Display title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe unique slug, auto-generated from title.
    /// Example: "getting-started-with-mwa"
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Primary content body. Format determined by the content type
    /// (Markdown, HTML, or plain text).
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Optional short excerpt/summary for list views and SEO.
    /// </summary>
    public string? Excerpt { get; set; }

    /// <summary>
    /// Structured fields as JSON, validated against the content type's field schema.
    /// Stores content-type-specific data beyond title/body
    /// (e.g., Post's FeaturedImageUrl, Faq's QuestionAnswerPairs).
    /// </summary>
    public string? FieldsJson { get; set; }

    /// <summary>
    /// Lifecycle status: Draft, Published, Archived.
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Author user ID.
    /// </summary>
    public DefaultId AuthorId { get; set; }

    /// <summary>
    /// Scheduled publish date. Content with status Draft and a future PublishAt
    /// can be auto-published by a worker (if enabled).
    /// </summary>
    public DateTime? PublishAt { get; set; }

    /// <summary>
    /// When this content was actually published (status changed to Published).
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Current revision number. Starts at 1, incremented on each update.
    /// </summary>
    public int RevisionNumber { get; set; } = 1;

    /// <summary>
    /// Optional ordering field for manual content ordering within a type.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Freeform metadata JSON (SEO fields, custom key-values).
    /// </summary>
    public string? MetadataJson { get; set; }

    // Navigations
    public ICollection<ContentItemTag> Tags { get; set; } = [];
    public ICollection<ContentMedia> Media { get; set; } = [];

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }
}
```

### `ContentRevision`

```csharp
public class ContentRevision
{
    public DefaultId Id { get; set; }
    public DefaultId ContentItemId { get; set; }

    /// <summary>
    /// Sequential revision number (1, 2, 3...).
    /// </summary>
    public int RevisionNumber { get; set; }

    /// <summary>
    /// Snapshot of the title at this revision.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the body at this revision.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Snapshot of the excerpt at this revision.
    /// </summary>
    public string? Excerpt { get; set; }

    /// <summary>
    /// Snapshot of the structured fields JSON at this revision.
    /// </summary>
    public string? FieldsJson { get; set; }

    /// <summary>
    /// Snapshot of the metadata JSON at this revision.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Optional change summary provided by the author.
    /// </summary>
    public string? ChangeNote { get; set; }

    /// <summary>
    /// Who created this revision.
    /// </summary>
    public DefaultId AuthorId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public ContentItem ContentItem { get; set; } = null!;
}
```

### `ContentTag`

```csharp
public class ContentTag : IAuditable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Display name (e.g., "Getting Started").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe slug (e.g., "getting-started"). Unique.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    // Navigations
    public ICollection<ContentItemTag> ContentItems { get; set; } = [];

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
```

### `ContentItemTag`

```csharp
public class ContentItemTag
{
    public DefaultId ContentItemId { get; set; }
    public DefaultId ContentTagId { get; set; }

    // Navigations
    public ContentItem ContentItem { get; set; } = null!;
    public ContentTag ContentTag { get; set; } = null!;
}
```

### `ContentMedia`

```csharp
public class ContentMedia : IAuditable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// The content item this media is attached to. Null for unattached/library media.
    /// </summary>
    public DefaultId? ContentItemId { get; set; }

    /// <summary>
    /// Original file name as uploaded.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Storage path/key in the Kernel's IFileStorage.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// MIME type (e.g., "image/jpeg", "application/pdf").
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Alt text for accessibility (images).
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Image width in pixels (null for non-image files).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Image height in pixels (null for non-image files).
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Public URL for accessing this media file.
    /// Constructed from FileStorageSettings.BaseUrl + StoragePath.
    /// </summary>
    public string? PublicUrl { get; set; }

    // Navigation
    public ContentItem? ContentItem { get; set; }

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }
}
```

---

## 2. Content Type System

Content types are **code-defined** (like Identity profiles), discovered at startup via assembly scanning, and registered in `ContentTypeRegistry`. Adding new content types is a code change — by design.

### `IContentType`

```csharp
/// <summary>
/// Defines a content type with its field schema.
/// Implementations are discovered by assembly scanning during module registration.
/// </summary>
public interface IContentType
{
    /// <summary>
    /// Unique name for this content type (e.g., "Page", "Post").
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    static abstract string Description { get; }

    /// <summary>
    /// Icon identifier for UI rendering (e.g., "page", "article", "help").
    /// </summary>
    static abstract string Icon { get; }

    /// <summary>
    /// Whether this content type uses the Body field (Markdown/HTML editor).
    /// Some types (e.g., FAQ) only use structured fields.
    /// </summary>
    static abstract bool HasBody { get; }

    /// <summary>
    /// Body format: "markdown", "html", or "plaintext".
    /// Only relevant when HasBody is true.
    /// </summary>
    static abstract string BodyFormat { get; }

    /// <summary>
    /// Custom field definitions beyond the standard title/body/excerpt.
    /// </summary>
    static abstract IReadOnlyList<ContentField> Fields { get; }

    /// <summary>
    /// Whether this content type supports tagging.
    /// </summary>
    static abstract bool SupportsTags { get; }

    /// <summary>
    /// Whether this content type supports media attachments.
    /// </summary>
    static abstract bool SupportsMedia { get; }

    /// <summary>
    /// Maximum number of revisions to retain. Null for unlimited.
    /// </summary>
    static abstract int? MaxRevisions { get; }
}
```

### `ContentField`

```csharp
public record ContentField(
    string Name,
    string DisplayName,
    ContentFieldType FieldType,
    bool IsRequired = false,
    string? DefaultValue = null,
    string? Description = null,
    int? MaxLength = null,
    string? ValidationRegex = null);

public enum ContentFieldType
{
    String,
    Text,
    Html,
    Markdown,
    Integer,
    Decimal,
    Boolean,
    DateTime,
    Url,
    Image,
    Json
}
```

### Built-In Content Types

#### `PageContentType`

```csharp
public class PageContentType : IContentType
{
    public static string Name => "Page";
    public static string Description => "A generic content page.";
    public static string Icon => "page";
    public static bool HasBody => true;
    public static string BodyFormat => "markdown";
    public static IReadOnlyList<ContentField> Fields => [];
    public static bool SupportsTags => true;
    public static bool SupportsMedia => true;
    public static int? MaxRevisions => null;
}
```

#### `PostContentType`

```csharp
public class PostContentType : IContentType
{
    public static string Name => "Post";
    public static string Description => "A blog post with featured image and excerpt.";
    public static string Icon => "article";
    public static bool HasBody => true;
    public static string BodyFormat => "markdown";
    public static IReadOnlyList<ContentField> Fields =>
    [
        new("FeaturedImageUrl", "Featured Image", ContentFieldType.Image),
        new("IsFeatured", "Featured Post", ContentFieldType.Boolean, DefaultValue: "false")
    ];
    public static bool SupportsTags => true;
    public static bool SupportsMedia => true;
    public static int? MaxRevisions => 50;
}
```

#### `FaqContentType`

```csharp
public class FaqContentType : IContentType
{
    public static string Name => "Faq";
    public static string Description => "FAQ entry with question and answer.";
    public static string Icon => "help";
    public static bool HasBody => false;
    public static string BodyFormat => "plaintext";
    public static IReadOnlyList<ContentField> Fields =>
    [
        new("Question", "Question", ContentFieldType.Text, IsRequired: true, MaxLength: 500),
        new("Answer", "Answer", ContentFieldType.Markdown, IsRequired: true)
    ];
    public static bool SupportsTags => true;
    public static bool SupportsMedia => false;
    public static int? MaxRevisions => 20;
}
```

### Creating Custom Content Types (Other Modules)

```csharp
// In a Documentation module
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

> **Discovery:** The `ContentTypeRegistry` scans the CMS module assembly during `CmsModule.Register()`. To register content types from other modules, use `ContentTypeRegistry.RegisterFromAssembly(assembly)` in the module's `Register()` method.

---

## 3. Revision System

Every update to a `ContentItem` automatically creates a `ContentRevision` before applying the changes. This provides a full edit history.

### Revision Lifecycle

1. **On create:** Revision #1 is created with the initial content.
2. **On update:** A new revision is created with the current (pre-update) content, then the `ContentItem` is updated and `RevisionNumber` is incremented.
3. **On rollback:** The `ContentItem` is updated with the revision's snapshot, creating a new revision (so rollback itself is tracked).

### Revision Comparison

The `CompareRevisions` feature produces a field-by-field diff between two revisions, indicating which fields changed and the old/new values.

```csharp
public record RevisionComparisonDto(
    int FromRevision,
    int ToRevision,
    IReadOnlyList<FieldDiff> Changes);

public record FieldDiff(
    string FieldName,
    string? OldValue,
    string? NewValue);
```

### Revision Pruning

If `IContentType.MaxRevisions` is set, the oldest revisions beyond the limit are automatically deleted when a new revision is created.

---

## 4. Slug Generation

### `ISlugService`

```csharp
public interface ISlugService
{
    /// <summary>
    /// Generates a URL-safe slug from a title.
    /// Handles Unicode transliteration, special character removal, and whitespace normalization.
    /// </summary>
    string Generate(string title);

    /// <summary>
    /// Ensures the slug is unique within the content type.
    /// Appends "-2", "-3", etc. if a conflict exists.
    /// </summary>
    Task<string> EnsureUniqueAsync(string slug, string contentType, DefaultId? excludeContentId = null, CancellationToken ct = default);
}
```

**Slug rules:**
- Lowercase
- Unicode characters transliterated to ASCII equivalents (e.g., `ñ` → `n`, `ü` → `u`)
- Whitespace and special characters replaced with hyphens
- Consecutive hyphens collapsed
- Leading/trailing hyphens trimmed
- Max length: 200 characters
- Uniqueness enforced per content type (not globally)

---

## 5. Persistence

### `CmsModuleDbContext`

```csharp
public class CmsModuleDbContext : DbContext, IDbContextSchema
{
    public string Schema => "cms";

    public DbSet<ContentItem> ContentItems { get; set; } = null!;
    public DbSet<ContentRevision> Revisions { get; set; } = null!;
    public DbSet<ContentTag> Tags { get; set; } = null!;
    public DbSet<ContentItemTag> ContentItemTags { get; set; } = null!;
    public DbSet<ContentMedia> Media { get; set; } = null!;
}
```

**Key indexes:**
- `ContentItem.(ContentType, Slug)` — unique composite
- `ContentItem.Status`
- `ContentItem.AuthorId`
- `ContentItem.PublishAt`
- `ContentItem.PublishedAt`
- `ContentItem.(ContentType, SortOrder)` — for ordered queries
- `ContentRevision.(ContentItemId, RevisionNumber)` — unique composite
- `ContentTag.Slug` — unique
- `ContentItemTag.(ContentItemId, ContentTagId)` — composite primary key
- `ContentMedia.ContentItemId`
- `ContentMedia.StoragePath` — unique

---

## 6. Services

### `IContentService`

```csharp
public interface IContentService
{
    Task<Result<ContentItem>> CreateAsync(string contentType, string title, string? body, string? excerpt,
        string? fieldsJson, DefaultId authorId, IEnumerable<DefaultId>? tagIds = null, CancellationToken ct = default);
    Task<Result<ContentItem>> GetByIdAsync(DefaultId id, CancellationToken ct = default);
    Task<Result<ContentItem>> GetBySlugAsync(string contentType, string slug, CancellationToken ct = default);
    Task<Result> UpdateAsync(DefaultId id, string? title, string? body, string? excerpt,
        string? fieldsJson, string? metadataJson, string? changeNote, CancellationToken ct = default);
    Task<Result> DeleteAsync(DefaultId id, CancellationToken ct = default);
    Task<Result> PublishAsync(DefaultId id, CancellationToken ct = default);
    Task<Result> UnpublishAsync(DefaultId id, CancellationToken ct = default);
    Task<Result> ArchiveAsync(DefaultId id, CancellationToken ct = default);
    Task<Result> RestoreRevisionAsync(DefaultId contentId, int revisionNumber, CancellationToken ct = default);
    Task<Result> SetTagsAsync(DefaultId contentId, IEnumerable<DefaultId> tagIds, CancellationToken ct = default);

    // Field validation
    Result ValidateFields(string contentType, string? fieldsJson);
}
```

### `IContentMediaService`

```csharp
public interface IContentMediaService
{
    Task<Result<ContentMedia>> UploadAsync(DefaultId? contentItemId, Stream fileStream, string fileName,
        string mimeType, string? altText = null, CancellationToken ct = default);
    Task<Result<IEnumerable<ContentMedia>>> GetByContentItemAsync(DefaultId contentItemId, CancellationToken ct = default);
    Task<Result> DeleteAsync(DefaultId mediaId, CancellationToken ct = default);
    Task<Result<Stream>> DownloadAsync(DefaultId mediaId, CancellationToken ct = default);
}
```

---

## 7. Configuration

### `appsettings.Cms.json`

```json
{
  "DefaultStatus": "Draft",
  "DefaultBodyFormat": "markdown",
  "MaxBodyLength": 500000,
  "MaxExcerptLength": 1000,
  "MaxTitleLength": 300,
  "MaxSlugLength": 200,
  "MaxFieldsJsonLength": 50000,
  "MaxMetadataJsonLength": 10000,
  "MaxTagsPerContent": 20,
  "MaxMediaPerContent": 50,
  "MaxMediaFileSizeMb": 25,
  "AllowedMediaMimeTypes": ["image/*", "application/pdf", "video/mp4"],
  "MediaStoragePath": "cms/media",
  "OrphanedMediaCleanupIntervalHours": 24,
  "OrphanedMediaGracePeriodHours": 2,
  "EnableScheduledPublishing": false,
  "ScheduledPublishingIntervalSeconds": 60
}
```

| Key | Type | Default | Purpose |
|---|---|---|---|
| `DefaultStatus` | `string` | `"Draft"` | Default status for newly created content |
| `DefaultBodyFormat` | `string` | `"markdown"` | Default body format when content type doesn't specify |
| `MaxBodyLength` | `int` | `500000` | Max body length in characters |
| `MaxExcerptLength` | `int` | `1000` | Max excerpt length |
| `MaxTitleLength` | `int` | `300` | Max title length |
| `MaxSlugLength` | `int` | `200` | Max slug length |
| `MaxFieldsJsonLength` | `int` | `50000` | Max structured fields JSON size |
| `MaxMetadataJsonLength` | `int` | `10000` | Max metadata JSON size |
| `MaxTagsPerContent` | `int` | `20` | Max tags per content item |
| `MaxMediaPerContent` | `int` | `50` | Max media attachments per content item |
| `MaxMediaFileSizeMb` | `int` | `25` | Max media file size in MB |
| `AllowedMediaMimeTypes` | `string[]` | `["image/*","application/pdf","video/mp4"]` | Allowed MIME types (wildcards supported) |
| `MediaStoragePath` | `string` | `"cms/media"` | Prefix path in Kernel's IFileStorage |
| `OrphanedMediaCleanupIntervalHours` | `int` | `24` | Cleanup worker interval for orphaned media |
| `OrphanedMediaGracePeriodHours` | `int` | `2` | Grace period before orphaned media is deleted |
| `EnableScheduledPublishing` | `bool` | `false` | Whether to auto-publish content at `PublishAt` |
| `ScheduledPublishingIntervalSeconds` | `int` | `60` | How often to check for content to auto-publish |

---

## 8. API Endpoints

### Content — Admin (`/api/v1/cms/content`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | ManageContent | Create content item |
| `GET` | `/` | ManageContent | List all content (paged, filterable by type, status, tag, author, date) |
| `GET` | `/{id}` | ManageContent | Get content by ID (includes drafts) |
| `PUT` | `/{id}` | ManageContent | Update content (creates revision) |
| `DELETE` | `/{id}` | ManageContent | Soft-delete content |
| `POST` | `/{id}/publish` | PublishContent | Publish a draft |
| `POST` | `/{id}/unpublish` | PublishContent | Revert to draft |
| `POST` | `/{id}/archive` | PublishContent | Archive content |
| `PUT` | `/{id}/tags` | ManageContent | Set tags for content item |
| `GET` | `/{id}/revisions` | ManageContent | List revisions for content item |
| `GET` | `/{id}/revisions/{number}` | ManageContent | Get specific revision |
| `POST` | `/{id}/revisions/{number}/restore` | ManageContent | Rollback to a revision |
| `GET` | `/{id}/revisions/compare` | ManageContent | Compare two revisions (?from=2&to=5) |

### Content — Public (`/api/v1/cms/public`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/content` | Anonymous | List published content (paged, filterable by type, tag) |
| `GET` | `/content/{type}/{slug}` | Anonymous | Get published content by type and slug |
| `GET` | `/tags` | Anonymous | List all tags with content counts |
| `GET` | `/tags/{slug}` | Anonymous | Get tag details with published content |

### Content Types (`/api/v1/cms/types`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/` | ManageContent | List all registered content types |
| `GET` | `/{name}` | ManageContent | Get content type details with field schema |

### Tags (`/api/v1/cms/tags`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | ManageContent | Create a tag |
| `GET` | `/` | ManageContent | List all tags (paged) |
| `GET` | `/{id}` | ManageContent | Get tag by ID |
| `PUT` | `/{id}` | ManageContent | Update tag name/description |
| `DELETE` | `/{id}` | ManageContent | Delete tag (removes from all content items) |

### Media (`/api/v1/cms/media`)

| Verb | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/` | ManageContent | Upload media (optional `contentItemId` query param) |
| `GET` | `/` | ManageContent | List all media (paged, filterable by content item, MIME type) |
| `GET` | `/{id}` | ManageContent | Get media metadata |
| `DELETE` | `/{id}` | ManageContent | Delete media (removes file from storage) |
| `GET` | `/content/{contentId}` | ManageContent | List media for a specific content item |

---

## 9. Cross-Module Contracts

### DTOs

```csharp
public record ContentItemDto(DefaultId Id, string ContentType, string Title, string Slug, string? Excerpt,
    string Status, DefaultId AuthorId, DateTime? PublishedAt, int RevisionNumber, DateTime CreatedAt);
public record ContentItemDetailDto(DefaultId Id, string ContentType, string Title, string Slug,
    string? Body, string? Excerpt, string? FieldsJson, string? MetadataJson,
    string Status, DefaultId AuthorId, DateTime? PublishAt, DateTime? PublishedAt,
    int RevisionNumber, int SortOrder,
    IReadOnlyList<ContentTagDto> Tags, IReadOnlyList<ContentMediaDto> Media, DateTime CreatedAt);
public record ContentRevisionDto(DefaultId Id, int RevisionNumber, DefaultId AuthorId, string? ChangeNote, DateTime CreatedAt);
public record ContentRevisionDetailDto(DefaultId Id, int RevisionNumber, string Title, string? Body,
    string? Excerpt, string? FieldsJson, string? MetadataJson, DefaultId AuthorId, string? ChangeNote, DateTime CreatedAt);
public record ContentTypeDto(string Name, string Description, string Icon, bool HasBody);
public record ContentTypeDetailDto(string Name, string Description, string Icon, bool HasBody, string BodyFormat,
    bool SupportsTags, bool SupportsMedia, int? MaxRevisions, IReadOnlyList<ContentFieldDto> Fields);
public record ContentFieldDto(string Name, string DisplayName, string FieldType, bool IsRequired,
    string? DefaultValue, string? Description, int? MaxLength, string? ValidationRegex);
public record ContentTagDto(DefaultId Id, string Name, string Slug, string? Description);
public record ContentMediaDto(DefaultId Id, string FileName, string MimeType, long SizeBytes,
    string? AltText, int? Width, int? Height, string? PublicUrl, DateTime CreatedAt);
public record RevisionComparisonDto(int FromRevision, int ToRevision, IReadOnlyList<FieldDiff> Changes);
public record FieldDiff(string FieldName, string? OldValue, string? NewValue);
```

### Events

```csharp
public record ContentCreatedEvent(DefaultId ContentId, string ContentType, string Title, string Slug, DefaultId AuthorId) : Event;
public record ContentPublishedEvent(DefaultId ContentId, string ContentType, string Slug, DefaultId PublishedBy) : Event;
public record ContentUnpublishedEvent(DefaultId ContentId, string ContentType, string Slug, DefaultId UnpublishedBy) : Event;
public record ContentArchivedEvent(DefaultId ContentId, string ContentType, string Slug, DefaultId ArchivedBy) : Event;
public record ContentUpdatedEvent(DefaultId ContentId, string ContentType, int RevisionNumber) : Event;
public record ContentDeletedEvent(DefaultId ContentId, string ContentType, string Slug, DefaultId DeletedBy) : Event;
public record MediaUploadedEvent(DefaultId MediaId, DefaultId? ContentId, string FileName, string MimeType, long SizeBytes) : Event;
```

### Cross-Module Requests

```csharp
public record GetContentBySlug(string ContentType, string Slug) : IRequest<ContentItemDetailDto>;
public record GetContentSummary(DefaultId ContentId) : IRequest<ContentItemDto>;
```

---

## 10. Workers

### `MediaCleanupWorker`

Periodic `BaseWorker` running at `OrphanedMediaCleanupIntervalHours` interval. Finds `ContentMedia` records where `ContentItemId IS NULL` and `CreatedAt < (now - GracePeriodHours)`, deletes the file from `IFileStorage`, and removes the database record.

> The grace period prevents deleting media that was uploaded but not yet attached to content (e.g., during a multi-step content creation flow).

---

## 11. Rules & Best Practices

1. **Content types are code, not config.** Add new content types as `IContentType` implementations. They are version-controlled and their field schemas are enforced at runtime.
2. **Never reference Cms implementation from other modules.** Reference `Cms.Contracts` only.
3. **Slugs are unique per content type, not globally.** `Page:getting-started` and `Post:getting-started` are distinct.
4. **Public endpoints only return Published content.** Drafts and archived content are only visible through admin endpoints.
5. **Field validation is enforced on create and update.** The `FieldsJson` is validated against the content type's field definitions. Missing required fields or type mismatches result in `400 Bad Request`.
6. **Revisions are immutable.** Once created, a revision is never modified. Rollback creates a new revision (the rollback itself is a new edit).
7. **Media uses Kernel's `IFileStorage`.** Files are stored under `MediaStoragePath` prefix. The `PublicUrl` is computed from `FileStorageSettings.BaseUrl`.
8. **Soft-deleted content retains its slug.** The uniqueness constraint excludes soft-deleted items, so a deleted slug can be reused.
9. **Use `SortOrder` for manual ordering.** For content types that need explicit ordering (e.g., documentation pages), use the `SortOrder` field rather than relying on creation date.
10. **All admin endpoints require `ManageContent` policy.** Publishing/unpublishing additionally requires `PublishContent` policy for separation of concerns (editors vs. publishers).
