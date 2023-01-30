using Prism.Mvvm;
using System;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Windows.Media;
using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.CustomAnalysis.Visualization.Properties;

namespace Cameca.CustomAnalysis.Visualization.Main3dChartVisualizer;

internal sealed class IonDisplay : BindableBase, IDisposable
{
	private const int DefaultDisplayCountMax = 100_000;
	private const int HardMaxRenderCount = 5_000_000;  // Never exceed this many rendered points

	public event EventHandler<RenderDataChangedEventArgs>? RenderDataChanged;

	private readonly int? _randomSeed;
	private bool _isDisplayPercentOverridden = false;

	private Vector3[] _shuffledPositions;
	private IPointsRenderData _renderData;
	private readonly IRenderDataFactory _renderDataFactory;
	private bool _globalIsVisible;

	[Display(AutoGenerateField = false)]
	public string Name { get; }

	private Color _color;
	[Display(AutoGenerateField = false)]
	public Color Color
	{
		get => _color;
		set => SetProperty(ref _color, value, UpdateRenderDataColor);
	}

	private bool _isVisible;
	[Display(ResourceType = typeof(Resources), Name = nameof(Resources.IonDisplayPropertiesIsVisibleLabel))]
	public bool IsVisible
	{
		get => _isVisible;
		set => SetProperty(ref _isVisible, value, UpdateRenderDataIsVisible);
	}

	private double _displayPercent = 100d;
	[Display(ResourceType = typeof(Resources), Name = nameof(Resources.IonDisplayPropertiesDisplayPercentLabel))]
	[DisplayFormat(DataFormatString = "F2", ApplyFormatInEditMode = true)]
	[Range(0d, 100d)]
	public double DisplayPercent
	{
		get => _displayPercent;
		set
		{
			_isDisplayPercentOverridden = true;
			UpdateDisplayPercent(value, true);
		}
	}

	[Display(ResourceType = typeof(Resources), Name = nameof(Resources.IonDisplayPropertiesDisplayCountLabel))]
	public int DisplayCount
	{
		get
		{
			double displayRatio = DisplayPercent / 100d;
			return (int)Math.Round(_shuffledPositions.Length * displayRatio);
		}
	}

	public IonDisplay(IRenderDataFactory renderDataFactory, string name, Color color, bool globalIsVisible, bool ionIsVisible = true, int? randomSeed = null)
	{
		_renderDataFactory = renderDataFactory;
		Name = name;
		_color = color;
		_globalIsVisible = globalIsVisible;
		_isVisible = ionIsVisible;
		_randomSeed = randomSeed;
		_shuffledPositions = Array.Empty<Vector3>();
		_renderData = CreateRenderData();
	}

	public void UpdatePositions(Vector3[] newPositions)
	{
		ArrayUtils.ShuffleInPlace(newPositions, CreateNewRandom());
		_shuffledPositions = newPositions;
		RecreateRenderData();
	}

	public void UpdateGlobalIsVisible(bool newGlobalIsVisible)
	{
		if (_globalIsVisible != newGlobalIsVisible)
		{
			_globalIsVisible = newGlobalIsVisible;
			UpdateRenderDataIsVisible();
		}
	}

	private void SetDisplayPercentForNewPositions()
	{
		var newIonCount = _shuffledPositions.Length;
		var newDisplayPercent = _isDisplayPercentOverridden
			? DisplayPercent
			: GetDefaultDisplayPercentForCount(newIonCount);
		UpdateDisplayPercent(newDisplayPercent, false);
	}

	private void UpdateDisplayPercent(double newValue, bool recreateRenderData)
	{
		double maxLimitedPercent = (((double)HardMaxRenderCount) / _shuffledPositions.Length) * 100d;
		if (SetProperty(ref _displayPercent, Math.Min(newValue, maxLimitedPercent), nameof(DisplayPercent)))
		{
			if (recreateRenderData)
			{
				RecreateRenderData();
			}
		}

		// Display count may change even if display percent doesn't due to capped values
		// Always and raise changed event. Calculation is trivial and event only triggers display updates
		RaisePropertyChanged(nameof(DisplayCount));

	}

	private void UpdateRenderDataIsVisible()
	{
		_renderData.IsVisible = GetRealVisibility();
	}

	private void UpdateRenderDataColor()
	{
		_renderData.Color = Color;
	}

	private IPointsRenderData CreateRenderData()
	{
		SetDisplayPercentForNewPositions();
		return _renderDataFactory.CreatePoints(
			_shuffledPositions[..DisplayCount],
			Color,
			Name,
			GetRealVisibility());
	}

	/// <summary>
	/// Default display percent is set to try to maintain either 100% of ions or
	/// the default visible max count, taking the lesser value. If positions are changes,
	/// therefore changing the total ion count, possibly update the default display percent.
	/// If the existing display percent was either 100%, or it was set such that the visible
	/// display count was the default maximum, then assume the display percent was unchanged
	/// by the user. Update the display percent to either show 100% or default max count.
	/// </summary>
	private double GetDefaultDisplayPercentForCount(int ionCount)
	{
		return Math.Min(100d, (((double)DefaultDisplayCountMax) / ionCount) * 100d);
	}

	private void RecreateRenderData()
	{
		var oldRenderData = _renderData;
		oldRenderData.Dispose();
		_renderData = CreateRenderData();
		RenderDataChanged?.Invoke(this, new RenderDataChangedEventArgs(oldRenderData, _renderData));
	}

	// Resolve real visibility value respecting both the global visibility state and the current ion visibility selection
	private bool GetRealVisibility()
	{
		// Turning off global visibility overrides individual ion selection
		if (!_globalIsVisible)
		{
			return false;
		}
		// If global visibility is enabled, then visibility is controlled by individual selection
		return IsVisible;
	}

	private Random CreateNewRandom() => _randomSeed.HasValue ? new Random(_randomSeed.Value) : new Random();

	public override string ToString() => "";

	public void Dispose()
	{
		_renderData.Dispose();
	}
}
