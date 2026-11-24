using Venture.Airo.Projects;

namespace Venture.Airo.Tests;

/// <summary>Small projects for the tests, written here so no test depends on Roundhouse's template or a shipped app.</summary>
internal static class SampleProjects
{
    /// <summary>
    /// A project in <paramref name="folder"/> whose app is a window that counts presses of <c>count</c> in
    /// <c>result</c>, greets whoever's submitted in <c>name</c>, hits an error on <c>boom</c>, and keeps <c>off</c>
    /// dimmed.
    /// </summary>
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
            local presses = 0
            window:setDisabled("off", true)
            window:on("count", function()
              presses = presses + 1
              window:setText("result", "Pressed " .. presses)
            end)
            window:on("name", function()
              window:setText("result", "Hello, " .. window:text("name"))
            end)
            window:on("boom", function()
              error("boom went off")
            end)

            """);
        Write(project.AppFolder, Path.Combine("Resources", "main.uix"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <UIScript version="1">
              <Window title="Sample" width="300" height="140"/>
              <Text name="result" x="12" y="12" width="276" height="20" text="Not pressed"/>
              <Button name="count" x="12" y="40" width="80" height="24" text="Count"/>
              <Button name="boom" x="100" y="40" width="80" height="24" text="Boom"/>
              <Button name="off" x="188" y="40" width="80" height="24" text="Off"/>
              <TextField name="name" x="12" y="76" width="276" height="24"/>
            </UIScript>

            """);
        return project;
    }

    /// <summary>Writes <paramref name="lua"/> as <c>Tests/<paramref name="file"/></c> in <paramref name="project"/>.</summary>
    public static void WithTest(RoundhouseProject project, string file, string lua) =>
        Write(Path.Combine(project.Root, "Tests"), file, lua);

    private static void Write(string folder, string relative, string text)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
