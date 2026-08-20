namespace WebsiteBuilder.Core.Models;

/// <summary>
/// The catalog of element kinds the editor can place on the canvas.
/// The string value (see <see cref="ElementTypes"/>) is what is persisted in
/// the Project JSON; this enum exists for strongly-typed code paths in C#.
/// New kinds are added here and in the React element registry together.
/// </summary>
public enum ElementType
{
    Unknown = 0,

    // Layout / structure
    Section,
    Container,
    Div,
    Navbar,
    Footer,
    Sidebar,
    Card,

    // Text
    Heading,
    Paragraph,
    Text,

    // Interactive
    Button,
    Link,

    // Media
    Image,
    Video,
    Audio,
    Icon,
    Svg,
    Canvas,

    // Forms
    Form,
    Input,
    Textarea,
    Checkbox,
    Radio,
    Dropdown,

    // Data / collections
    Table,
    List,

    // Composite widgets
    Accordion,
    Tabs,
    Modal,
    Alert,
    Badge,
    Avatar,
    ProgressBar,
    Spinner,
    Breadcrumb,
    Pagination,

    // Embeds / custom
    GoogleMap,
    YouTube,
    CustomHtml,
    CustomJavaScript,
    CustomCss,
}

/// <summary>
/// Canonical string identifiers for element types as stored in Project JSON.
/// Kept in one place so the C# enum and the persisted contract never drift.
/// </summary>
public static class ElementTypes
{
    public const string Section = "Section";
    public const string Container = "Container";
    public const string Div = "Div";
    public const string Navbar = "Navbar";
    public const string Footer = "Footer";
    public const string Sidebar = "Sidebar";
    public const string Card = "Card";
    public const string Heading = "Heading";
    public const string Paragraph = "Paragraph";
    public const string Text = "Text";
    public const string Button = "Button";
    public const string Link = "Link";
    public const string Image = "Image";
    public const string Video = "Video";
    public const string Audio = "Audio";
    public const string Icon = "Icon";
    public const string Svg = "Svg";
    public const string Canvas = "Canvas";
    public const string Form = "Form";
    public const string Input = "Input";
    public const string Textarea = "Textarea";
    public const string Checkbox = "Checkbox";
    public const string Radio = "Radio";
    public const string Dropdown = "Dropdown";
    public const string Table = "Table";
    public const string List = "List";
    public const string Accordion = "Accordion";
    public const string Tabs = "Tabs";
    public const string Modal = "Modal";
    public const string Alert = "Alert";
    public const string Badge = "Badge";
    public const string Avatar = "Avatar";
    public const string ProgressBar = "ProgressBar";
    public const string Spinner = "Spinner";
    public const string Breadcrumb = "Breadcrumb";
    public const string Pagination = "Pagination";
    public const string GoogleMap = "GoogleMap";
    public const string YouTube = "YouTube";
    public const string CustomHtml = "CustomHtml";
    public const string CustomJavaScript = "CustomJavaScript";
    public const string CustomCss = "CustomCss";
}
