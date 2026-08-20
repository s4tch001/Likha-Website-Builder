using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.ViewModels.Panels;

/// <summary>
/// Lists the pages that make up the current project. Stays in sync with the
/// active project via <see cref="IProjectService.CurrentChanged"/>. Page add /
/// rename / delete commands are layered on as the editor matures.
/// </summary>
public sealed partial class ProjectExplorerViewModel : ToolViewModel
{
    private readonly IProjectService _projects;

    public ProjectExplorerViewModel(IProjectService projects)
        : base(PanelIds.ProjectExplorer, "Project")
    {
        _projects = projects;
        _projects.CurrentChanged += (_, project) => Load(project);
        _projects.Mutated += (_, project) => Load(project);

        if (_projects.Current is not null)
        {
            Load(_projects.Current);
        }
    }

    public ObservableCollection<Page> Pages { get; } = new();

    [ObservableProperty]
    private Page? _selectedPage;

    [ObservableProperty]
    private string _projectName = "No project";

    /// <summary>Re-reads the current project (e.g. after starter content is applied).</summary>
    public void Reload()
    {
        if (_projects.Current is not null)
        {
            Load(_projects.Current);
        }
    }

    private void Load(Project project)
    {
        ProjectName = project.Name;
        Pages.Clear();
        foreach (var page in project.Pages)
        {
            Pages.Add(page);
        }

        SelectedPage = Pages.FirstOrDefault();
    }
}
