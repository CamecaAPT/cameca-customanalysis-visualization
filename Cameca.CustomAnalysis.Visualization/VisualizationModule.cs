using Cameca.CustomAnalysis.Utilities;
using Prism.Ioc;
using Prism.Modularity;

namespace Cameca.CustomAnalysis.Visualization;

/// <summary>
/// Public <see cref="IModule"/> implementation is the entry point for AP Suite to discover and configure the custom analysis
/// </summary>
public class VisualizationModule : IModule
{
	public void RegisterTypes(IContainerRegistry containerRegistry)
	{
		containerRegistry.AddCustomAnalysisUtilities(options => options.UseStandardBaseClasses = true);
	}

	public void OnInitialized(IContainerProvider containerProvider)
	{
	}
}
