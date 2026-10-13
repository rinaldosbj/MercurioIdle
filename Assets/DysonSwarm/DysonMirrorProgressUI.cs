using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates an authored prefab; never constructs UI elements.</summary>
[DisallowMultipleComponent]
public sealed class DysonMirrorProgressUI : MonoBehaviour
{
    [SerializeField] private DysonSwarmRenderer swarm;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text countLabel;
    private double previousCount = double.NaN, previousGoal = double.NaN;
    private double previousPercent = double.NaN;
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("pt-BR");

    private void OnEnable()
    {
        previousCount = previousGoal = previousPercent = double.NaN;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (swarm == null || progressSlider == null || countLabel == null) return;
        double current = FiniteNonnegative(swarm.MirrorCount);
        double maximum = FiniteNonnegative(swarm.MaximumRealMirrors);
        double progress = maximum > 0 ? Math.Min(1, current / maximum) : 0;
        progressSlider.SetValueWithoutNotify((float)progress);
        double count = Math.Floor(current);
        double goal = Math.Floor(maximum);
        double percent = Math.Round(progress * 100, 2);
        if (count == previousCount && goal == previousGoal && percent == previousPercent) return;
        previousCount = count;
        previousGoal = goal;
        previousPercent = percent;
        countLabel.text = count.ToString("N0", NumberCulture) + " / " + goal.ToString("N0", NumberCulture)
            + "  [" + percent.ToString("0.00", NumberCulture) + "%]";
    }

    private static double FiniteNonnegative(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, value);
}
