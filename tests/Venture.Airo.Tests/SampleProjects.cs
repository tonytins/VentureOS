using Venture.Airo.Projects;

namespace Venture.Airo.Tests;

/// <summary>Small projects for the tests, written here so no test depends on Roundhouse's template or a shipped app.</summary>
internal static class SampleProjects
{
    /// <summary>A project in <paramref name="folder"/> whose app is a window with a button, as New App makes.</summary>
    public static RoundhouseProject WithApp(string folder, string name)
    {
        var project = RoundhouseProject.Create(name, folder);
        Write(project.AppFolder, "Info.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <App version="1">
              <Info identifier="dev.example.{name.ToLowerInvariant().Replace(' ', '-')}" name="{name}" version="1.0" description="A sample."/>
            </App>

            """);
        Write(project.AppFolder, Path.Combine("Lua", "main.lua"), """
            local window = venture.ui.open("main.uix")

            """);
        Write(project.AppFolder, Path.Combine("Resources", "main.uix"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <UIScript version="1">
              <Window title="Sample" width="200" height="100"/>
              <Button name="roll" text="Roll" x="20" y="20" width="80" height="24"/>
            </UIScript>

            """);
        return project;
    }

    private static void Write(string folder, string relative, string text)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
