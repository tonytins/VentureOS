using Venture.Airo.Projects;

namespace Venture.Airo.Tests;

/// <summary>Small projects for the tests, written here so no test depends on Roundhouse's template or a shipped app.</summary>
internal static class SampleProjects
{
    /// <summary>
    /// A project in <paramref name="folder"/> whose app is a window that counts presses of <c>count</c> (or the
    /// <c>add</c> toolbar item) in <c>result</c>, which the <c>reset</c> menu item sets back; greets whoever's
    /// submitted in <c>name</c>; says which <c>fruits</c> row, or <c>looks</c> grid item, was chosen; says when <c>body</c> was typed in or
    /// <c>place</c>, its title field, was submitted; counts its timer's ticks in <c>ticks</c>; hits an error on
    /// <c>boom</c>; keeps <c>off</c> dimmed; and opens a window for New Window and for a file.
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
            local ticks = 0
            window:setDisabled("off", true)
            window:setItems("fruits", { "apple", "pear" })
            local function count()
              presses = presses + 1
              window:setText("result", "Pressed " .. presses)
            end
            window:on("count", count)
            window:on("add", count)
            window:on("reset", function()
              presses = 0
              window:setText("result", "Not pressed")
            end)
            window:on("fruits", function()
              window:setText("result", "Chose row " .. window:selected("fruits"))
            end)
            window:on("looks", function(event)
              window:setText("result", "Chose " .. event.item)
            end)
            window:on("body", function(event)
              if event.kind == "changed" then
                window:setText("result", "Typed " .. window:text("body"))
              end
            end)
            window:on("place", function()
              window:setText("result", "Went to " .. window:text("place"))
            end)
            window:every(1, function()
              ticks = ticks + 1
              window:setText("ticks", tostring(ticks))
            end)
            venture.ui.onNewWindow(function() venture.ui.open("main.uix") end)
            venture.ui.onOpenFile(function(path)
              local other = venture.ui.open("main.uix")
              other:setText("result", "Opened " .. path)
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
              <Window title="Sample" width="300" height="300"/>
              <TitleField name="place" placeholder="Where"/>
              <ToolbarItem name="add" title="Add"/>
              <Menu name="sampleMenu" title="Sample">
                <MenuItem name="reset" title="Reset"/>
              </Menu>
              <Text name="result" x="12" y="12" width="276" height="20" text="Not pressed"/>
              <Button name="count" x="12" y="40" width="80" height="24" text="Count"/>
              <Button name="boom" x="100" y="40" width="80" height="24" text="Boom"/>
              <Button name="off" x="188" y="40" width="80" height="24" text="Off"/>
              <TextField name="name" x="12" y="76" width="276" height="24"/>
              <List name="fruits" x="12" y="108" width="276" height="60" selectable="true"/>
              <TextArea name="body" x="12" y="176" width="276" height="60"/>
              <Text name="ticks" x="12" y="244" width="276" height="20" text="0"/>
              <Grid name="looks" x="12" y="268" width="276" height="28" itemWidth="20" itemHeight="20" titles="none">
                <Item name="light" title="Light" color="#FFFFFF"/>
                <Item name="dark" title="Dark" color="#000000"/>
              </Grid>
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
