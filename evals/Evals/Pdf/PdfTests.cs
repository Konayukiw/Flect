using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Evals.Pdf;

[Collection(Serial.Name)]
public sealed class PdfTests
{
    [Theory]
    [InlineData("PNG", ".png")]
    [InlineData("JPG", ".jpg")]
    public async Task ConvertRendersEveryPage(string target, string extension)
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("pdf-render");
        var input = await Fixtures.CopyAsync("alpha.pdf", folder);

        await Rig.RunCleanAsync($"pdf.convert?to={target}", input);

        Assert.True(File.Exists(Path.Combine(folder, "alpha_p1" + extension)));
        Assert.True(File.Exists(Path.Combine(folder, "alpha_p2" + extension)));
    }

    [Fact]
    public async Task ConvertToTextExtractsTheWords()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("pdf-text");
        var input = await Fixtures.CopyAsync("alpha.pdf", folder);

        await Rig.RunCleanAsync("pdf.convert?to=TXT", input);

        var text = await File.ReadAllTextAsync(Path.Combine(folder, "alpha.txt"), TestContext.Current.CancellationToken);
        Assert.Contains("alpha page 1", text);
        Assert.Contains("alpha page 2", text);
    }

    [Fact]
    public async Task MergeFollowsTheChosenOrder()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("pdf-merge");
        var alpha = await Fixtures.CopyAsync("alpha.pdf", folder);
        var bravo = await Fixtures.CopyAsync("bravo.pdf", folder);
        var task = Rig.Create("pdf.merge", alpha, bravo);
        Rig.Set(task, "_sources", new[] { bravo, alpha });

        var progress = await Rig.RunAsync(task, configure: false, token: TestContext.Current.CancellationToken);

        progress.AssertClean();
        using var merged = PdfDocument.Open(Path.Combine(folder, "bravo_merged.pdf"));
        Assert.Equal(4, merged.NumberOfPages);
        Assert.Contains("bravo page 1", merged.GetPage(1).Text);
        Assert.Contains("alpha page 1", merged.GetPage(3).Text);
    }
}
