using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.CustomAnalysis.Visualization.Main3dChartVisualizer;
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
		containerRegistry.AddCustomAnalysisUtilities(options => options.UseBaseClasses = true);

		containerRegistry.Register<object, Main3dChartNode>(Main3dChartNode.UniqueId);
		containerRegistry.RegisterInstance(Main3dChartNode.DisplayInfo, Main3dChartNode.UniqueId);
		containerRegistry.Register<IAnalysisMenuFactory, Main3dChartNodeMenuFactory>(nameof(Main3dChartNodeMenuFactory));
	}

	public void OnInitialized(IContainerProvider containerProvider)
	{
	}
}
