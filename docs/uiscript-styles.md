# UIScript styles (draft)

This is a draft revision of the [UIScript format](https://github.com/tonytins/Hudini/blob/main/doc/UISCRIPT.md)'s
**Styles** section, which Hudini defines. It's written as if it were already
in the spec: VentureOS reads it today, and Hudini's styling rework can take
it from here.

A style should say what a color or size is *for*, not what it is, so a
document follows the host's look: light or dark, any accent, and whatever
the host's theme becomes later. The same idea let Mac apps that used
semantic colors take on Dark Mode with no changes. Colors written as hex
still work, and each has a `dark` twin for a dark palette.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<UIScript version="1">
  <Window width="300" height="120"/>

  <Style name="heading" fontSize="title"/>
  <Style name="quiet" textColor="secondaryText"/>
  <Style name="display" fontSize="24" backgroundColor="inputBackground"/>
  <Style name="brand" textColor="#1E5AA0" darkTextColor="#8CB8F0"/>

  <Text name="title" x="10" y="10" width="280" height="24" style="heading" text="Downloads"/>
  <Text name="status" x="10" y="40" width="280" height="20" style="quiet" text="3 items"/>
</UIScript>
```

## Styles

`<Style>` is a named set of visual properties that controls refer to.

| Attribute | Value |
| --- | --- |
| `name` | Required. The name controls use. |
| `textColor`, `backgroundColor`, `borderColor` | Colors used when drawing. |
| `selectedTextColor`, `highlightedTextColor`, `disabledTextColor` | Button label colors while pressed, hovered, and disabled. |
| `fillColor` | The filled part of a progress bar and the thumb of a slider. |
| `darkTextColor`, `darkBackgroundColor`, and the rest | A color's `dark` twin: the same attribute with `dark` before it, used in place of it on a dark palette. |
| `fontSize` | A size role, or a positive size in pixels. Without one, text uses the `body` size. |
| `font` | `monospace` for the host's fixed-width font. Without one, text uses the interface font. |

### Colors

A color is one of:

- **A role**, the color the host's palette gives that purpose. Prefer roles:
  they follow light and dark, the accent, and later themes.
- **`#RRGGBB` or `#RRGGBBAA`**, for a color that must be exactly that, such as
  a brand's. It's the same in every palette unless a `dark` twin replaces it.

| Role | For |
| --- | --- |
| `text` | Ordinary text. |
| `secondaryText` | Quieter text, such as a status line. |
| `disabledText` | Text on something turned off. |
| `highlightedText` | Text on the accent color. |
| `accent` | The accent: focus, selection marks, and fills. |
| `panel` | A window's background. |
| `inputBackground` | The inside of text fields and lists. |
| `border` | Lines around controls. |
| `selection` | A selected item's background. |

### Sizes

| Role | VentureOS's size |
| --- | --- |
| `caption` | 11 |
| `body` | 13, the size text takes without a style |
| `title` | 17 |
| `largeTitle` | 22 |

A host chooses its own sizes for the roles; the table shows VentureOS's.

## Rules

- A color that's neither a role nor `#` and six or eight hex digits refuses
  the document, as does a `fontSize` that's neither a role nor a positive
  whole number.
- A `dark` twin may name a role or a hex color. It's optional, and may be
  given without its light color, in which case the light palette uses the
  host's default.
- On a light palette, `dark` twins are ignored. On a dark one, each twin that
  is set replaces its color; the others apply as written.
- Roles and size roles are part of the color and size grammar, not a
  `requires` feature. A reader from before them refuses a document that uses
  one as an invalid color or size, which says plainly that it needs a newer
  reader.

## Host responsibilities

- Resolve roles against the palette in use each time it draws, or again when
  the palette changes, so switching light, dark, or the accent reaches every
  open window without the document changing.
- Keep the shipped roles readable together: each text role against each
  background role it's used on, in every palette the host offers. VentureOS
  tests every interface it ships for a contrast of at least 3 to 1 in every
  palette.
- Use a document's hex colors exactly as written, `dark` twins included; the
  document has asked for that color.
