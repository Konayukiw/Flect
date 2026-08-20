namespace Optimizer.Main.Config;

internal enum AppTheme { System, Light, Dark }

internal enum AppLanguage { System, English, Japanese, ChineseSimplified }

internal enum DuplicateStrictness { SizeOnly, HeadHash, FullContent }

internal enum DuplicateKeep { Ask, Oldest, Newest }

internal enum DeleteMethod { RecycleBin, Permanent }

internal enum VideoCodecChoice { H264, H265, Vp9, Av1 }

internal enum VideoContainer { Auto, Mp4, Mkv, WebM }

internal enum HardwareEncoder { None, Nvenc, Qsv, Amf }

internal enum EncodePriority { Speed, Balanced, Quality }

internal enum GifDither { None, Bayer, FloydSteinberg }

internal enum TrimAccuracy { Keyframe, Exact }

internal enum ThumbnailFormat { Png, Jpg }

internal enum ArchiveCompressFormat { Zip, SevenZip, Tar, TarGz }

internal enum BackgroundRemovalMode { AiWithFallback, AiOnly, ChromaKeyOnly }

internal enum SjisFallback { ReplaceAndWarn, Substitute, Fail }

internal enum JsonKeyOrder { Ordinal, IgnoreCase, Natural }

internal enum OcrOutput { TextFile, Clipboard, ShowOnly }

internal enum TextEncodingChoice { Utf8, Utf8Bom, Utf16, ShiftJis }

internal enum ReportFormat { Csv, Json, Html, Txt }

internal sealed class ExclusionRules
{
    public string Folders { get; set; } = string.Empty;
    public string Files { get; set; } = string.Empty;
    public string Extensions { get; set; } = string.Empty;
}

internal sealed class GeneralSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public AppLanguage Language { get; set; } = AppLanguage.System;

    public bool CloseOnSuccess { get; set; } = true;
    public bool CloseOnFailure { get; set; }
    public bool CloseOnCancel { get; set; } = true;

    public List<string> HiddenMenuItems { get; set; } = [];
}

internal sealed class DuplicateSettings
{
    public DuplicateStrictness Strictness { get; set; } = DuplicateStrictness.FullContent;
    public bool MatchName { get; set; }
    public bool MatchTimestamp { get; set; }
    public bool IgnoreEmptyFiles { get; set; } = true;
    public long MinimumBytes { get; set; }
    public DuplicateKeep Keep { get; set; } = DuplicateKeep.Ask;
}

internal sealed class RenameSettings
{
    public bool UseNumbering { get; set; } = true;
    public int DigitCount { get; set; }
    public int StartNumber { get; set; } = 1;
    public bool DryRun { get; set; }
    public ExclusionRules Exclusions { get; set; } = new();
}

internal sealed class EmptyFolderSettings
{
    public bool IgnoreZeroByteFiles { get; set; }
    public bool IgnoreDesktopIni { get; set; } = true;
    public bool IgnoreThumbsDb { get; set; } = true;
}

internal sealed class TreeSettings
{
    public bool FoldersOnly { get; set; }
    public ExclusionRules Exclusions { get; set; } = new();
}

internal sealed class AnalyzeSettings
{
    public ReportFormat SaveFormat { get; set; } = ReportFormat.Csv;
    public ExclusionRules Exclusions { get; set; } = new();
}

internal sealed class ArchiveSettings
{
    public ArchiveCompressFormat CompressFormat { get; set; } = ArchiveCompressFormat.Zip;
}

internal sealed class FolderSettings
{
    public DuplicateSettings Duplicates { get; set; } = new();
    public DeleteMethod DeleteMethod { get; set; } = DeleteMethod.RecycleBin;
    public ExclusionRules ScanExclusions { get; set; } = new();
    public RenameSettings Rename { get; set; } = new();
    public EmptyFolderSettings Empty { get; set; } = new();
    public TreeSettings Tree { get; set; } = new();
    public AnalyzeSettings Analyze { get; set; } = new();
    public ArchiveSettings Archive { get; set; } = new();
}

internal sealed class VideoPresets
{
    public double ResizeA { get; set; } = 50;
    public double ResizeB { get; set; } = 25;

    public long CompressDiscord { get; set; } = 10L * 1024 * 1024;
    public long CompressA { get; set; } = 50L * 1024 * 1024;
    public long CompressB { get; set; } = 25L * 1024 * 1024;
    public long CompressC { get; set; } = 5L * 1024 * 1024;

    public double RotateA { get; set; } = 45;
    public double RotateB { get; set; } = 90;
    public double RotateC { get; set; } = 180;
}

internal sealed class TrimSettings
{
    public TrimAccuracy Accuracy { get; set; } = TrimAccuracy.Keyframe;
}

internal sealed class ThumbnailSettings
{
    public ThumbnailFormat Format { get; set; } = ThumbnailFormat.Png;
    public int Quality { get; set; } = 90;
    public int MaxWidth { get; set; }
}

internal sealed class VideoSettings
{
    public VideoPresets Presets { get; set; } = new();
    public TrimSettings Trim { get; set; } = new();
    public ThumbnailSettings Thumbnail { get; set; } = new();
    public VideoCodecChoice Codec { get; set; } = VideoCodecChoice.H264;
    public VideoContainer Container { get; set; } = VideoContainer.Auto;
    public HardwareEncoder Hardware { get; set; } = HardwareEncoder.None;
    public EncodePriority Priority { get; set; } = EncodePriority.Balanced;

    public int GifFps { get; set; } = 15;
    public int GifMaxWidth { get; set; } = 640;
    public GifDither GifDither { get; set; } = GifDither.Bayer;

    public string RotateFillColor { get; set; } = "#000000";
}

internal sealed class ImagePresets
{
    public double ResizeA { get; set; } = 50;
    public double ResizeB { get; set; } = 25;

    public long CompressA { get; set; } = 10L * 1024 * 1024;
    public long CompressB { get; set; } = 5L * 1024 * 1024;
    public long CompressC { get; set; } = 3L * 1024 * 1024;
    public long CompressD { get; set; } = 1L * 1024 * 1024;

    public double RotateA { get; set; } = 45;
    public double RotateB { get; set; } = 90;
    public double RotateC { get; set; } = 180;
}

internal sealed class MetadataSettings
{
    public bool RemoveExif { get; set; } = true;
    public bool RemoveGps { get; set; } = true;
    public bool RemoveComments { get; set; } = true;
    public bool RemoveColorProfile { get; set; }
    public bool ApplyOrientation { get; set; } = true;
}

internal sealed class ImageSettings
{
    public ImagePresets Presets { get; set; } = new();

    public bool AllowWebpFallback { get; set; } = true;
    public int WebpQuality { get; set; } = 80;

    public BackgroundRemovalMode BackgroundMode { get; set; } = BackgroundRemovalMode.AiWithFallback;
    public string BackgroundKeyColor { get; set; } = "#00FF00";
    public double BackgroundTolerance { get; set; } = 25;

    public MetadataSettings Metadata { get; set; } = new();
}

internal sealed class JsonSortSettings
{
    public JsonKeyOrder Comparison { get; set; } = JsonKeyOrder.Ordinal;
    public bool Descending { get; set; }
    public bool Recursive { get; set; } = true;
    public bool SortPrimitiveArrays { get; set; }
    public string PinnedKeys { get; set; } = string.Empty;
}

internal sealed class TextSettings
{
    public SjisFallback SjisFallback { get; set; } = SjisFallback.ReplaceAndWarn;
    public string SjisSubstitute { get; set; } = "?";
    public JsonSortSettings JsonSort { get; set; } = new();
}

internal sealed class OcrSettings
{
    public string Language { get; set; } = string.Empty;
    public OcrOutput Output { get; set; } = OcrOutput.TextFile;
    public TextEncodingChoice Encoding { get; set; } = TextEncodingChoice.Utf8Bom;
    public bool SuppressLanguageNotice { get; set; }
}

internal sealed class Settings
{
    private const string FileName = "settings.json";

    private static Settings? _current;

    public GeneralSettings General { get; set; } = new();
    public FolderSettings Folder { get; set; } = new();
    public VideoSettings Video { get; set; } = new();
    public ImageSettings Image { get; set; } = new();
    public TextSettings Text { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();

    public static Settings Current => _current ??= Load();

    public static Settings Load() => Store.Read<Settings>(FileName) ?? new Settings();

    public void Save()
    {
        Store.Write(FileName, this);
        ShellBridge.Publish(this);
        _current = this;
    }

    public static void Reset()
    {
        Store.Delete(FileName);
        ShellBridge.Clear();
        _current = new Settings();
    }
}
