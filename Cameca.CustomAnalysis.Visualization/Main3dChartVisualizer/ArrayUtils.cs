using System;

namespace Cameca.CustomAnalysis.Visualization.Main3dChartVisualizer;

internal static class ArrayUtils
{
	public static void ShuffleInPlace<T>(T[] array, Random? random = null)
	{
		random ??= new Random();
		int n = array.Length;
		for (int i = 0; i < (n - 1); i++)
		{
			int r = i + random.Next(n - i);
			(array[r], array[i]) = (array[i], array[r]);
		}
	}

}
