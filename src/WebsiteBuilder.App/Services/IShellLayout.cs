namespace WebsiteBuilder.App.Services;

/// <summary>
/// View-level layout operations the shell window exposes to its view model.
/// These manipulate the AvalonDock layout (a pure view concern), so they live
/// behind an interface implemented by the window and are invoked by commands.
/// </summary>
public interface IShellLayout
{
    /// <summary>Shows or hides a dockable panel identified by its ContentId.</summary>
    void TogglePanel(string contentId);

    /// <summary>Brings a panel into view and activates it.</summary>
    void ShowPanel(string contentId);

    /// <summary>Restores the default panel arrangement.</summary>
    void ResetLayout();
}
