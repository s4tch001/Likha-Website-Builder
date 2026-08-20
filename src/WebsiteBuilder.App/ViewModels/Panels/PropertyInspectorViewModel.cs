using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.App.ViewModels.Panels;

/// <summary>
/// One editable custom-CSS row (a property name and value, including CSS variables).
/// Editing the value or removing the row pushes the change via the supplied callback.
/// </summary>
public sealed partial class StyleRow : ObservableObject
{
    private readonly Action<string, string> _apply;

    public StyleRow(string name, string value, Action<string, string> apply)
    {
        Name = name;
        _apply = apply;
        _value = value; // set backing field directly so the setter doesn't push on load
    }

    /// <summary>The CSS property name (read-only; remove + re-add to rename).</summary>
    public string Name { get; }

    [ObservableProperty]
    private string _value;

    partial void OnValueChanged(string value) => _apply(Name, value);

    /// <summary>Clears the style (empty value removes it on the editor side).</summary>
    [RelayCommand]
    private void Remove() => _apply(Name, string.Empty);
}

/// <summary>
/// The Property Inspector. Phase 7a adds a live, two-way editable Layout group
/// (position, size, rotation, opacity) for a single selected element; remaining
/// styles are listed read-only. Edits are pushed to the editor over the bridge,
/// and the inspector refreshes from the editor's selection echo (guarded against
/// feedback loops). Typography / appearance / advanced groups arrive in 7b+.
/// </summary>
public sealed partial class PropertyInspectorViewModel : ToolViewModel
{
    private readonly EditorSession _editor;
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;

    // True while loading values from the editor, so property setters do not echo
    // the change straight back and cause a feedback loop.
    private bool _suppressPush;
    private string? _elementId;

    public PropertyInspectorViewModel(EditorSession editor)
        : base(PanelIds.PropertyInspector, "Properties")
    {
        _editor = editor;
        editor.SelectionChanged += (_, node) =>
        {
            if (_uiDispatcher.CheckAccess())
            {
                Load(node);
            }
            else
            {
                _uiDispatcher.Invoke(() => Load(node));
            }
        };
    }

    /// <summary>Editable rows for styles not covered by the structured fields (incl. CSS variables).</summary>
    public ObservableCollection<StyleRow> OtherStyles { get; } = new();

    /// <summary>Properties overridden at the active (non-base) breakpoint; each row resets via remove.</summary>
    public ObservableCollection<StyleRow> Overrides { get; } = new();

    /// <summary>True when editing a non-base breakpoint (the overrides section is shown).</summary>
    [ObservableProperty]
    private bool _isBreakpointOverride;

    /// <summary>Label of the breakpoint currently being authored (e.g. "Mobile").</summary>
    [ObservableProperty]
    private string _breakpointLabel = string.Empty;

    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>True when exactly one element is selected (the editable form is shown).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isSingle;

    /// <summary>True when the single selected element is locked (editing disabled).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isLocked;

    /// <summary>Whether the property fields are editable (a single, unlocked element).</summary>
    public bool CanEdit => IsSingle && !IsLocked;

    [ObservableProperty]
    private string _selectionTitle = "No selection";

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;
    [ObservableProperty] private double _rotation;

    /// <summary>Opacity as a percentage (0–100).</summary>
    [ObservableProperty] private double _opacity = 100;

    // --- Content & typography (raw CSS values) ---
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private string _fontSize = string.Empty;
    [ObservableProperty] private string _fontWeight = string.Empty;
    [ObservableProperty] private string _lineHeight = string.Empty;
    [ObservableProperty] private string _letterSpacing = string.Empty;
    [ObservableProperty] private string _textColor = string.Empty;
    [ObservableProperty] private string _textAlign = string.Empty;

    // --- Appearance ---
    [ObservableProperty] private string _background = string.Empty;
    [ObservableProperty] private string _borderWidth = string.Empty;
    [ObservableProperty] private string _borderStyle = string.Empty;
    [ObservableProperty] private string _borderColor = string.Empty;
    [ObservableProperty] private string _borderRadius = string.Empty;
    [ObservableProperty] private string _boxShadow = string.Empty;
    [ObservableProperty] private string _blendMode = string.Empty;

    // --- Advanced ---
    [ObservableProperty] private string _overflow = string.Empty;
    [ObservableProperty] private string _zIndex = string.Empty;
    [ObservableProperty] private string _visibility = string.Empty;
    [ObservableProperty] private string _cursor = string.Empty;
    [ObservableProperty] private string _filter = string.Empty;
    [ObservableProperty] private string _transition = string.Empty;

    // --- Custom CSS ---
    [ObservableProperty] private string _newPropertyName = string.Empty;
    [ObservableProperty] private string _newPropertyValue = string.Empty;

    public string[] OverflowOptions { get; } = { "", "visible", "hidden", "scroll", "auto", "clip" };
    public string[] VisibilityOptions { get; } = { "", "visible", "hidden" };
    public string[] CursorOptions { get; } = { "", "default", "pointer", "text", "move", "grab", "not-allowed" };

    /// <summary>Preset options for the font-weight dropdown.</summary>
    public string[] FontWeightOptions { get; } =
        { "", "normal", "300", "400", "500", "600", "700", "800", "bold" };

    /// <summary>Preset options for the text-align dropdown.</summary>
    public string[] TextAlignOptions { get; } = { "", "left", "center", "right", "justify" };

    /// <summary>Preset options for the border-style dropdown.</summary>
    public string[] BorderStyleOptions { get; } = { "", "none", "solid", "dashed", "dotted", "double" };

    /// <summary>Preset options for the mix-blend-mode dropdown.</summary>
    public string[] BlendModeOptions { get; } =
        { "", "normal", "multiply", "screen", "overlay", "darken", "lighten", "difference" };

    private static readonly HashSet<string> BorderStyleKeywords = new(StringComparer.Ordinal)
    {
        "none", "solid", "dashed", "dotted", "double", "groove", "ridge", "inset", "outset", "hidden",
    };

    /// <summary>Style keys covered by structured fields (excluded from the raw list).</summary>
    private static readonly HashSet<string> StructuredStyleKeys = new(StringComparer.Ordinal)
    {
        "opacity", "font-size", "font-weight", "line-height", "letter-spacing", "color", "text-align",
        "background", "border", "border-radius", "box-shadow", "mix-blend-mode",
        "overflow", "z-index", "visibility", "cursor", "filter", "transition",
    };

    private void Load(ElementNode? node)
    {
        _suppressPush = true;

        if (node is null || _editor.SelectedCount > 1)
        {
            _elementId = null;
            IsSingle = false;
            IsLocked = false;
            HasSelection = _editor.SelectedCount > 0;
            SelectionTitle = _editor.SelectedCount > 1
                ? $"{_editor.SelectedCount} elements selected"
                : "No selection";
            OtherStyles.Clear();
            Overrides.Clear();
            IsBreakpointOverride = false;
            BreakpointLabel = string.Empty;
            _suppressPush = false;
            return;
        }

        _elementId = node.Id;
        HasSelection = true;
        IsSingle = true;
        IsLocked = node.Locked;
        SelectionTitle = string.IsNullOrWhiteSpace(node.Name) ? $"{node.Type} · {node.Id}" : node.Name!;

        X = node.X;
        Y = node.Y;
        Width = node.Width;
        Height = node.Height;
        Rotation = node.Rotation;
        Opacity = ReadOpacity(node);

        Text = node.Text ?? string.Empty;
        FontSize = Style(node, "font-size");
        FontWeight = Style(node, "font-weight");
        LineHeight = Style(node, "line-height");
        LetterSpacing = Style(node, "letter-spacing");
        TextColor = Style(node, "color");
        TextAlign = Style(node, "text-align");

        Background = Style(node, "background");
        ParseBorder(Style(node, "border"));
        BorderRadius = Style(node, "border-radius");
        BoxShadow = Style(node, "box-shadow");
        BlendMode = Style(node, "mix-blend-mode");

        Overflow = Style(node, "overflow");
        ZIndex = Style(node, "z-index");
        Visibility = Style(node, "visibility");
        Cursor = Style(node, "cursor");
        Filter = Style(node, "filter");
        Transition = Style(node, "transition");

        OtherStyles.Clear();
        foreach (var (name, value) in node.Styles)
        {
            if (!StructuredStyleKeys.Contains(name))
            {
                OtherStyles.Add(new StyleRow(name, value, ApplyCustomStyle));
            }
        }

        // Breakpoint override context: list the properties overridden at the active
        // non-base breakpoint, each removable (reset) via the StyleRow's clear path.
        IsBreakpointOverride = !_editor.IsBaseBreakpoint;
        BreakpointLabel = _editor.SelectedBreakpointLabel ?? string.Empty;
        Overrides.Clear();
        if (IsBreakpointOverride)
        {
            foreach (var key in _editor.OverriddenKeys)
            {
                var value = node.Styles.TryGetValue(key, out var v) ? v : string.Empty;
                Overrides.Add(new StyleRow(key, value, ApplyCustomStyle));
            }
        }

        _suppressPush = false;
    }

    /// <summary>Clears every override at the active breakpoint (reverting to inherited values).</summary>
    [RelayCommand]
    private void ResetAllOverrides()
    {
        if (_elementId is null)
        {
            return;
        }

        // Snapshot keys first; clearing each removes it from the editor's override layer.
        foreach (var key in _editor.OverriddenKeys.ToArray())
        {
            _editor.SetStyle(_elementId, key, string.Empty);
        }
    }

    /// <summary>Pushes an edited/removed custom style row to the editor.</summary>
    private void ApplyCustomStyle(string name, string value)
    {
        if (_elementId is not null && !string.IsNullOrWhiteSpace(name))
        {
            _editor.SetStyle(_elementId, name, value);
        }
    }

    private static string Style(ElementNode node, string key)
        => node.Styles.TryGetValue(key, out var value) ? value : string.Empty;

    private static double ReadOpacity(ElementNode node)
    {
        if (node.Styles.TryGetValue("opacity", out var raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return Math.Clamp(value * 100, 0, 100);
        }

        return 100;
    }

    partial void OnXChanged(double value) => PushGeometry(x: value);
    partial void OnYChanged(double value) => PushGeometry(y: value);
    partial void OnWidthChanged(double value) => PushGeometry(width: value);
    partial void OnHeightChanged(double value) => PushGeometry(height: value);

    partial void OnRotationChanged(double value)
    {
        if (!_suppressPush && _elementId is not null)
        {
            _editor.SetRotation(_elementId, value);
        }
    }

    partial void OnOpacityChanged(double value)
    {
        if (!_suppressPush && _elementId is not null)
        {
            var css = (value / 100).ToString("0.###", CultureInfo.InvariantCulture);
            _editor.SetStyle(_elementId, "opacity", css);
        }
    }

    partial void OnTextChanged(string value)
    {
        if (!_suppressPush && _elementId is not null)
        {
            _editor.SetText(_elementId, value);
        }
    }

    partial void OnFontSizeChanged(string value) => PushStyle("font-size", value);
    partial void OnFontWeightChanged(string value) => PushStyle("font-weight", value);
    partial void OnLineHeightChanged(string value) => PushStyle("line-height", value);
    partial void OnLetterSpacingChanged(string value) => PushStyle("letter-spacing", value);
    partial void OnTextColorChanged(string value) => PushStyle("color", value);
    partial void OnTextAlignChanged(string value) => PushStyle("text-align", value);

    partial void OnBackgroundChanged(string value) => PushStyle("background", value);
    partial void OnBorderRadiusChanged(string value) => PushStyle("border-radius", value);
    partial void OnBoxShadowChanged(string value) => PushStyle("box-shadow", value);
    partial void OnBlendModeChanged(string value) => PushStyle("mix-blend-mode", value);

    partial void OnOverflowChanged(string value) => PushStyle("overflow", value);
    partial void OnZIndexChanged(string value) => PushStyle("z-index", value);
    partial void OnVisibilityChanged(string value) => PushStyle("visibility", value);
    partial void OnCursorChanged(string value) => PushStyle("cursor", value);
    partial void OnFilterChanged(string value) => PushStyle("filter", value);
    partial void OnTransitionChanged(string value) => PushStyle("transition", value);

    /// <summary>Adds the custom CSS property typed by the user (supports CSS variables like --brand).</summary>
    [RelayCommand]
    private void AddCustomProperty()
    {
        var name = NewPropertyName.Trim();
        if (_elementId is null || string.IsNullOrEmpty(name))
        {
            return;
        }

        _editor.SetStyle(_elementId, name, NewPropertyValue.Trim());
        NewPropertyName = string.Empty;
        NewPropertyValue = string.Empty;
    }

    partial void OnBorderWidthChanged(string value) => PushBorder();
    partial void OnBorderStyleChanged(string value) => PushBorder();
    partial void OnBorderColorChanged(string value) => PushBorder();

    /// <summary>Composes the border shorthand from the three sub-fields and pushes it.</summary>
    private void PushBorder()
    {
        if (_suppressPush || _elementId is null)
        {
            return;
        }

        var parts = new[] { BorderWidth, BorderStyle, BorderColor }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        _editor.SetStyle(_elementId, "border", string.Join(" ", parts).Trim());
    }

    /// <summary>Splits a CSS border shorthand into width / style / color sub-fields.</summary>
    private void ParseBorder(string border)
    {
        BorderWidth = string.Empty;
        BorderStyle = string.Empty;
        BorderColor = string.Empty;

        if (string.IsNullOrWhiteSpace(border))
        {
            return;
        }

        var colorParts = new List<string>();
        foreach (var token in border.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (BorderStyleKeywords.Contains(token))
            {
                BorderStyle = token;
            }
            else if (token.Length > 0 && (char.IsDigit(token[0]) || token[0] == '.'))
            {
                BorderWidth = token;
            }
            else
            {
                colorParts.Add(token);
            }
        }

        BorderColor = string.Join(" ", colorParts);
    }

    private void PushStyle(string name, string value)
    {
        if (!_suppressPush && _elementId is not null)
        {
            _editor.SetStyle(_elementId, name, value ?? string.Empty);
        }
    }

    private void PushGeometry(double? x = null, double? y = null, double? width = null, double? height = null)
    {
        if (!_suppressPush && _elementId is not null)
        {
            _editor.SetGeometry(_elementId, x, y, width, height);
        }
    }
}
