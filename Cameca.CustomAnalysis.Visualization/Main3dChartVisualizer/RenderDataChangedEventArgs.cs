using System;
using Cameca.CustomAnalysis.Interface;

namespace Cameca.CustomAnalysis.Visualization.Main3dChartVisualizer;

public class RenderDataChangedEventArgs : EventArgs
{
	public IRenderData OldValue { get; }
	public IRenderData NewValue { get; }

	public RenderDataChangedEventArgs(IRenderData oldValue, IRenderData newValue)
	{
		OldValue = oldValue;
		NewValue = newValue;
	}
}
