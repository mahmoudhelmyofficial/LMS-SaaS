using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إحصائيات مكتبة الوسائط - Media statistics view model
/// </summary>
public class MediaStatisticsViewModel
{
    public int TotalMediaFiles { get; set; }
    public int TotalFiles { get => TotalMediaFiles; set => TotalMediaFiles = value; }
    public int TotalVideos { get; set; }
    public int TotalImages { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalAudios { get; set; }
    public long TotalStorageUsedBytes { get; set; }
    public long TotalSize { get => TotalStorageUsedBytes; set => TotalStorageUsedBytes = value; }
    public long TotalStorageUsedMB { get; set; }
    public long TotalStorageUsedGB { get; set; }
    public decimal StorageUsagePercentage { get; set; }
    public decimal StoragePercentage { get => StorageUsagePercentage; set => StorageUsagePercentage = value; }
    public int UploadsThisMonth { get; set; }
    public int UploadsThisWeek { get; set; }
    public int UploadsToday { get; set; }
    public int TotalViews { get; set; }
    public int TotalDownloads { get; set; }
    public List<MediaFileSummary> RecentUploads { get; set; } = new();
    public List<MediaFileSummary> RecentFiles { get => RecentUploads; set => RecentUploads = value; }
    public List<MediaFileSummary> LargestFiles { get; set; } = new();
    public List<MediaFileSummary> TopDownloadedFiles { get; set; } = new();
    public List<MediaFileSummary> TopViewedFiles { get; set; } = new();
    public Dictionary<string, int> FilesByType { get; set; } = new();
    public List<FileTypeStat> FileTypeStats { get; set; } = new();
    public Dictionary<string, long> StorageByType { get; set; } = new();
    public List<TimelineEntry> UploadTimeline { get; set; } = new();
    public List<ActivityEntry> ActivityData { get; set; } = new();
}

/// <summary>
/// File type statistics
/// </summary>
public class FileTypeStat
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public long Size { get; set; }
    public long TotalSize { get => Size; set => Size = value; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Timeline entry for upload statistics
/// </summary>
public class TimelineEntry
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
    public long Size { get; set; }
}

/// <summary>
/// Activity entry for activity data
/// </summary>
public class ActivityEntry
{
    public string Date { get; set; } = string.Empty;
    public int Views { get; set; }
    public int Downloads { get; set; }
}

/// <summary>
/// ملخص الملف - Media file summary
/// </summary>
public class MediaFileSummary
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long FileSize { get => FileSizeBytes; set => FileSizeBytes = value; }
    public string FileSizeMB { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime CreatedAt { get => UploadedAt; set => UploadedAt = value; }
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int DownloadCount { get; set; }
}

