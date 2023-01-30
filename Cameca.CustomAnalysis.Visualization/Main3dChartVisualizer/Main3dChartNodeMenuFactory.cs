using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Events;

namespace Cameca.CustomAnalysis.Visualization.Main3dChartVisualizer;

internal class Main3dChartNodeMenuFactory : AnalysisMenuFactoryBase
{
	public Main3dChartNodeMenuFactory(IEventAggregator eventAggregator)
		: base(eventAggregator)
	{
	}

	protected override INodeDisplayInfo DisplayInfo => Main3dChartNode.DisplayInfo;
	protected override string NodeUniqueId => Main3dChartNode.UniqueId;
	public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;
}
