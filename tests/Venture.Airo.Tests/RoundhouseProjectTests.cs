using Venture.OS.Bundles;
using Venture.Airo.Projects;

namespace Venture.Airo.Tests;

/// <summary>
/// <see cref="RoundhouseProject"/>: a <c>.rproj</c> made, opened, and refused when it isn't one this reads, and its
/// app assembled into an <c>.app</c> VentureOS reads, with unsaved edits in place of what's saved.
/// </summary>
public sealed class RoundhouseProjectTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-project-{Guid.NewGuid():N}");

    public RoundhouseProjectTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void ANewProjectIsEmptyAndOpensAgain()
    {
        var project = RoundhouseProject.Create("Tom & Jerry's Dice", folder);

        Assert.Equal(Path.Combine(folder, "Tom & Jerry's Dice.rproj"), project.Root);
        Assert.Equal(["Project.xml"], Directory.GetFileSystemEntries(project.Root).Select(Path.GetFileName));
        Assert.False(project.HasApp);
        Assert.Equal("Tom & Jerry's Dice", RoundhouseProject.Open(project.Root).Name);
    }

    [Theory]
    [InlineData("", "A project needs a name.")]
    [InlineData("a/b", "since it names the project's folder")]
    [InlineData(".hidden", "can't start with a dot")]
    public void RefusesANameThatCantBeAFolder(string name, string message)
    {
        var problem = Assert.Throws<ProjectException>(() => RoundhouseProject.Create(name, folder));

        Assert.Contains(message, problem.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public void RefusesToMakeOneThatsThere()
    {
        RoundhouseProject.Create("Dice", folder);

        Assert.Contains("already Dice.rproj", Assert.Throws<ProjectException>(() => RoundhouseProject.Create("Dice", folder)).Message);
    }

    [Theory]
    [InlineData(null, "has no Project.xml")]
    [InlineData("<Project version=\"2\"><Info name=\"Dice\"/></Project>", "format version 2 project, and this Roundhouse reads up to 1")]
    [InlineData("<Project version=\"one\"><Info name=\"Dice\"/></Project>", "isn't a format version")]
    [InlineData("<App version=\"1\"><Info name=\"Dice\"/></App>", "starts with <App>, not <Project>")]
    [InlineData("<Project version=\"1\"><Info name=\"Dice\"/><Build target=\"x\"/></Project>", "<Build> isn't something this Roundhouse knows")]
    [InlineData("<Project version=\"1\"><Info/></Project>", "<Info> needs a name")]
    [InlineData("<Project version=\"1\"></Project>", "has no <Info>")]
    [InlineData("<Project version=\"1\"><Info name=\"Dice\">", "Project.xml, line")]
    public void RefusesWhatItCantRead(string? text, string message)
    {
        var root = Path.Combine(folder, "Dice.rproj");
        Directory.CreateDirectory(root);
        if (text is not null)
            File.WriteAllText(Path.Combine(root, "Project.xml"), text);

        Assert.Contains(message, Assert.Throws<ProjectException>(() => RoundhouseProject.Open(root)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAttributeItDoesntKnowIsIgnored()
    {
        var root = Path.Combine(folder, "Dice.rproj");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Project.xml"), "<Project version=\"1\" theme=\"dark\"><Info name=\"Dice\" icon=\"x\"/></Project>");

        Assert.Equal("Dice", RoundhouseProject.Open(root).Name);
    }

    [Fact]
    public void FindsTheProjectAFileIsIn()
    {
        var project = RoundhouseProject.Create("Dice", folder);

        Assert.Equal(project.Root, RoundhouseProject.Find(Path.Combine(project.Root, "App", "Resources", "main.uix")));
        Assert.Equal(project.Root, RoundhouseProject.Find(project.Root));
        Assert.Null(RoundhouseProject.Find(folder));
    }

    [Fact]
    public void AssemblesItsAppIntoAnAppVentureOSReads()
    {
        var project = WithApp("Dice");
        var output = Path.Combine(folder, "out");

        var app = project.Assemble(output);

        Assert.Equal(Path.Combine(output, "Dice.app"), app);
        Assert.Equal("Dice", Bundle.Read(app.Replace('\\', '/'), BundleKind.App).Name);
        Assert.True(File.Exists(Path.Combine(app, "Contents", "Resources", "main.uix")));
        Assert.False(Directory.Exists(Path.Combine(project.Root, "Dice.app")));
    }

    [Fact]
    public void UnsavedEditsGoInPlaceOfWhatsSaved()
    {
        var project = WithApp("Dice");
        var lua = Path.Combine(project.AppFolder, "Lua", "main.lua");
        var info = Path.Combine(project.AppFolder, "Info.xml");
        var edited = File.ReadAllText(info).Replace("name=\"Dice\"", "name=\"Dice Roller\"", StringComparison.Ordinal);

        var app = project.Assemble(folder, new Dictionary<string, string>
        {
            [lua] = "-- edited\n",
            [info] = edited,
            [Path.Combine(project.InterfacesFolder, "open.uix")] = "not the app's",
        });

        // The edited Info.xml names the .app, and only files under App/ go in it.
        Assert.Equal(Path.Combine(folder, "Dice Roller.app"), app);
        Assert.Equal("-- edited\n", File.ReadAllText(Path.Combine(app, "Contents", "Lua", "main.lua")));
        Assert.False(File.Exists(Path.Combine(app, "Contents", "open.uix")));
        Assert.NotEqual("-- edited\n", File.ReadAllText(lua));
    }

    [Fact]
    public void ReplacesAnAppThatsThereOnlyWhenAsked()
    {
        var project = WithApp("Dice");
        var app = project.Assemble(folder);
        File.WriteAllText(Path.Combine(app, "stale.txt"), "old");

        Assert.Contains("already Dice.app", Assert.Throws<ProjectException>(() => project.Assemble(folder)).Message);
        Assert.True(File.Exists(Path.Combine(app, "stale.txt")));

        Assert.Equal(app, project.Assemble(folder, replace: true));
        Assert.False(File.Exists(Path.Combine(app, "stale.txt")));
        Assert.Equal(["Dice.app", "Dice.rproj"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnAppVentureOSWouldRefuseIsntLeftBehind()
    {
        var project = WithApp("Dice");
        var info = Path.Combine(project.AppFolder, "Info.xml");
        File.WriteAllText(info, File.ReadAllText(info).Replace("identifier=", "main=\"nowhere.lua\" identifier=", StringComparison.Ordinal));
        var output = Path.Combine(folder, "out");
        Directory.CreateDirectory(output);

        Assert.Throws<ProjectException>(() => project.Assemble(output));
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Theory]
    [InlineData("<App version=\"1\"><Info identifier=\"dev.example.dice\"/></App>", "has no <Info name=")]
    [InlineData("<App version=\"1\"><Info name=\"a/b\" identifier=\"dev.example.dice\"/></App>", "can't name the app's folder")]
    [InlineData("<App version=\"1\"><Info name=\"Dice\"", "App/Info.xml, line")]
    public void RefusesAnAppWithoutAUsableName(string info, string message)
    {
        var project = WithApp("Dice");
        File.WriteAllText(Path.Combine(project.AppFolder, "Info.xml"), info);

        Assert.Contains(message, Assert.Throws<ProjectException>(() => project.Assemble(folder)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AProjectWithoutAnAppSaysSo()
    {
        var project = RoundhouseProject.Create("Dialogs", folder);

        Assert.Contains("has no app yet", Assert.Throws<ProjectException>(() => project.Assemble(folder)).Message);
    }

    private RoundhouseProject WithApp(string name) => SampleProjects.WithApp(folder, name);
}
