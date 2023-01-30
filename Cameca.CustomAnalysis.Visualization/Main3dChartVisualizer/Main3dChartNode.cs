using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.Input;

namespace Cameca.CustomAnalysis.Visualization.Main3dChartVisualizer;

internal class Main3dChartNode : AnalysisNodeBase
{
	public const string UniqueId = "Cameca.CustomAnalysis.Visualization.Main3dChartVisualizer.Main3dChartNode";
	private const string UnknownName = "Unranged";
	private static readonly Color UnknownColor = Colors.DarkGray;

	public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("3D Chart Visualizer");

	private readonly INodeVisibilityControlProvider _nodeVisibilityControlProvider;
	private readonly IRenderDataFactory _renderDataFactory;
	private readonly IMainChartProvider _mainChartProvider;
	private readonly INodeMenuFactoryProvider _nodeMenuFactoryProvider;
	private readonly IIonDisplayInfoProvider _ionDisplayInfoProvider;
	private readonly INodePropertiesProvider _nodePropertiesProvider;
	private INodeVisibilityControl? _nodeVisibilityControl = null;

	private IIonDisplayInfo? _ionDisplayInfo = null;
	private IIonDisplayInfo IonDisplayInfo => _ionDisplayInfo ??= _ionDisplayInfoProvider.Resolve(InstanceId)
	                                                              ?? throw new InvalidOperationException("Could not resolve IonDisplayInfo");

	private IChart3D? _mainChart;
	private IChart3D MainChart => _mainChart ??= _mainChartProvider.Resolve(InstanceId)
	                                             ?? throw new InvalidOperationException("Could not resolve MainChart");


	private readonly List<IonDisplay> _ionDisplay = new();

	private readonly AsyncRelayCommand _renderIonsCommand;
	public ICommand RenderIonsCommand => _renderIonsCommand;

	public Main3dChartNode(
		INodeVisibilityControlProvider nodeVisibilityControlProvider,
		IRenderDataFactory renderDataFactory,
		IMainChartProvider mainChartProvider,
		INodeMenuFactoryProvider nodeMenuFactoryProvider,
		IIonDisplayInfoProvider ionDisplayInfoProvider,
		INodePropertiesProvider nodePropertiesProvider,
		IAnalysisNodeBaseServices services) : base(services)
	{
		_nodeVisibilityControlProvider = nodeVisibilityControlProvider;
		_renderDataFactory = renderDataFactory;
		_mainChartProvider = mainChartProvider;
		_nodeMenuFactoryProvider = nodeMenuFactoryProvider;
		_ionDisplayInfoProvider = ionDisplayInfoProvider;
		_nodePropertiesProvider = nodePropertiesProvider;

		_renderIonsCommand = new AsyncRelayCommand(Refresh);
	}

	protected override void OnAdded(NodeAddedEventArgs eventArgs)
	{
		_nodeVisibilityControl = _nodeVisibilityControlProvider.Resolve(InstanceId);
		if (_nodeVisibilityControl is not null)
		{
			_nodeVisibilityControl.IsEnabled = true;
			_nodeVisibilityControl.PropertyChanged += OnNodeVisibilityControlPropertyChanged;
			_nodeVisibilityControl.IsVisible = true;
		}

		if (_nodeMenuFactoryProvider.Resolve(InstanceId) is { } nodeMenuFactory)
		{
			nodeMenuFactory.ContextMenuItems = new List<IMenuItem>
			{
				new MenuAction("Refresh", RenderIonsCommand),
			};
		}

		if (_nodePropertiesProvider.Resolve(InstanceId) is { } nodeProperties)
		{
			nodeProperties.Properties = new ExpandoObject();
		}

		if (DataState is not null)
		{
			DataState.PropertyChanged += DataStateOnPropertyChanged;
		}

		RenderIonsCommand.Execute(null);
	}

	protected override void OnDoubleClick()
	{
		if (!DataStateIsValid)
		{
			RenderIonsCommand.Execute(null);
		}
	}

	private void OnNodeVisibilityControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(INodeVisibilityControl.IsVisible))
		{
			var newVisibility = _nodeVisibilityControl?.IsVisible ?? true;
			foreach (var ionDisplay in _ionDisplay)
			{
				ionDisplay.UpdateGlobalIsVisible(newVisibility);
			}
		}
	}

	/// <summary>
	/// Triggered when the data state changed.
	/// Handle external changes that invalidate the current state,
	/// and appropriately invalidate the current ion render data.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	private void DataStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (DataState is null || !DataState.IsValid || DataState.IsErrorState)
		{
			InvalidateCurrentIonData();
		}
	}

	private void InvalidateCurrentIonData()
	{
		foreach (var ionDisplay in _ionDisplay)
		{
			ionDisplay.UpdatePositions(Array.Empty<Vector3>());
		}
	}

	private async Task Refresh(CancellationToken token)
	{
		if (await GetIonData(cancellationToken: token) is not { } ionData)
		{
			return;
		}

		UpdateIonDisplayCollection(ionData);
		UpdatePropertiesObject();
		await UpdateIonPositions(ionData, token);
		DataStateIsValid = true;
	}

	private void UpdateIonDisplayCollection(IIonData ionData)
	{
		List<string> trackedIonNames = _ionDisplay.Select(x => x.Name).ToList();
		List<string> ionDataNames = ionData.Ions.Select(x => x.Name).Concat(new[] { UnknownName }).ToList();
		var needRemoval = trackedIonNames.Except(ionDataNames);
		var needUpdate = trackedIonNames.Intersect(ionDataNames);
		var needAdding = ionDataNames.Except(trackedIonNames);
		foreach (var key in needRemoval)
		{
			var removeItem = _ionDisplay.Single(x => x.Name == key);
			removeItem.RenderDataChanged -= NewIonDisplayOnRenderDataChanged;
			_ionDisplay.Remove(removeItem);
			removeItem.Dispose();
		}
		foreach (var key in needUpdate)
		{
			// Assume unknown ion type if not found
			var ionTypeInfo = ionData.Ions.SingleOrDefault(x => x.Name == key);
			var ionDisplay = _ionDisplay.Single(x => x.Name == key);
			ionDisplay.Color = GetColor(ionTypeInfo);
		}
		foreach (var key in needAdding)
		{
			// Assume unknown ion type if not found
			var ionTypeInfo = ionData.Ions.SingleOrDefault(x => x.Name == key);
			var newIonDisplay = new IonDisplay(
				_renderDataFactory,
				key,
				GetColor(ionTypeInfo),
				_nodeVisibilityControl?.IsVisible ?? true,
				ionIsVisible: key != UnknownName,
				0);  // Default visible unless unknown ions
			newIonDisplay.RenderDataChanged += NewIonDisplayOnRenderDataChanged;
			_ionDisplay.Add(newIonDisplay);
		}
	}

	private Color GetColor(IIonTypeInfo? ionTypeInfo)
	{
		return ionTypeInfo is not null ? IonDisplayInfo.GetColor(ionTypeInfo) : UnknownColor;
	}

	private void NewIonDisplayOnRenderDataChanged(object? sender, RenderDataChangedEventArgs e)
	{
		MainChart.DataSource.Remove(e.OldValue);
		MainChart.DataSource.Add(e.NewValue);
	}

	private void UpdatePropertiesObject()
	{
		if (_nodePropertiesProvider.Resolve(InstanceId) is not { Properties: IDictionary<string, object> dynamicProperties })
		{
			return;
		}

		List<string> currentIonNames = _ionDisplay.Select(x => x.Name).ToList();
		// Remove ions that no longer are included
		var needRemoval = dynamicProperties.Keys.Except(currentIonNames);
		foreach (var key in needRemoval)
		{
			dynamicProperties.Remove(key);
		}
		// Add ions that are missing
		var needAdding = currentIonNames.Except(dynamicProperties.Keys).ToHashSet();
		foreach (var ionInfoName in needAdding)
		{
			dynamicProperties[ionInfoName] = _ionDisplay.Single(x => x.Name == ionInfoName);
		}
	}
	
	private async Task UpdateIonPositions(IIonData ionData, CancellationToken token)
	{
		var requiredSections = new string[] { IonDataSectionName.Position, IonDataSectionName.IonType };
		if (!await ionData.EnsureSectionsAvailable(requiredSections, null, cancellationToken: token))
		{
			return;
		}

		var newIonPositions = CreateNewPositionArrays(ionData, requiredSections);

		foreach (var ionDisplay in _ionDisplay)
		{
			if (newIonPositions.TryGetValue(ionDisplay.Name, out var newPositions))
			{
				ionDisplay.UpdatePositions(newPositions);
			}
		}
	}

	private IDictionary<string, Vector3[]> CreateNewPositionArrays(IIonData ionData, string[] sections)
	{
		// Create position buffers of appropriate size for all ions (ranged and unranged) in this IonData instance
		ulong unknownIonCount = ionData.IonCount;  // To determine unknown count, start with full count and subtract ionic counts
		var ionPositions = new Vector3[ionData.Ions.Count + 1][];
		var names = new string[ionData.Ions.Count + 1];
		int ionIndex = 0;
		var ionCounts = ionData.GetIonTypeCounts();
		foreach (var ionType in ionData.Ions)
		{
			names[ionIndex] = ionType.Name;
			unknownIonCount -= ionCounts[ionType];
			ionPositions[ionIndex++] = new Vector3[ionCounts[ionType]];
		}
		// UnknownName
		names[ionIndex] = UnknownName;
		ionPositions[ionIndex] = new Vector3[unknownIonCount];

		var currentIonIndices = new int[ionPositions.Length];
		foreach (var chunk in ionData.CreateSectionDataEnumerable(sections))
		{
			var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position).Span;
			var ionTypes = chunk.ReadSectionData<byte>(IonDataSectionName.IonType).Span;

			for (int i = 0; i < chunk.Length; i++)
			{
				int ionId = ionTypes[i] < 255 ? ionTypes[i] : ionIndex;
				ionPositions[ionId][currentIonIndices[ionId]++] = positions[i];
			}
		}

		return names.Zip(ionPositions)
			.ToDictionary(x => x.First, x => x.Second);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (_nodeVisibilityControl is not null)
			{
				_nodeVisibilityControl.PropertyChanged -= OnNodeVisibilityControlPropertyChanged;
			}

			MainChart.DataSource.DisposeAndClear();
		}
	}
}
