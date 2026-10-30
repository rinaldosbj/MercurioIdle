using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public sealed class ProbeFactory : Building
    {
        private const float BarWidth = 1.6f;
        private const float BarHeight = 0.1f;
        private const float LaunchHeight = 7f;
        private const float LaunchSeconds = 1.4f;

        public static readonly List<ProbeFactory> All = new List<ProbeFactory>();

        [SerializeField, Min(1f)] private float secondsPerProbe = 50f;

        private float progress;
        private GameObject bar;
        private Transform barFill;

        public float SecondsPerProbe => secondsPerProbe;

        public static float TotalRatePerSecond
        {
            get
            {
                float total = 0f;
                foreach (var factory in All) total += 1f / factory.secondsPerProbe;
                return total;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFactories() => All.Clear();

        public static ProbeFactory Create(Vector3 basePosition, float fall)
        {
            return Spawn<ProbeFactory>("Fábrica de sondas", ProbeFactoryArt.GetSprite(), ProbeFactoryArt.WorldWidth, ProbeFactoryArt.WorldHeight, basePosition, fall);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!All.Contains(this)) All.Add(this);
        }

        protected override void OnDisable()
        {
            All.Remove(this);
            base.OnDisable();
        }

        protected override void OnBuilt()
        {
            bar = new GameObject("Progresso");
            bar.transform.SetParent(transform, false);
            bar.transform.localPosition = new Vector3(0f, Height + 0.35f, -0.05f);

            var back = new GameObject("Fundo");
            back.transform.SetParent(bar.transform, false);
            back.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
            var backRenderer = back.AddComponent<SpriteRenderer>();
            backRenderer.sprite = ShapeRaster.GetSquareSprite();
            backRenderer.color = ShapeRaster.Gray(60);
            backRenderer.sortingOrder = 5;

            var fill = new GameObject("Preenchimento");
            fill.transform.SetParent(bar.transform, false);
            barFill = fill.transform;
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = ShapeRaster.GetSquareSprite();
            fillRenderer.color = ShapeRaster.Gray(242);
            fillRenderer.sortingOrder = 6;

            UpdateBar();
            bar.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();
            if (!Landed) return;

            if (!bar.activeSelf) bar.SetActive(true);

            progress += Time.deltaTime;
            if (progress >= secondsPerProbe)
            {
                progress -= secondsPerProbe;
                Produce();
            }

            UpdateBar();
        }

        private void Produce()
        {
            ProbeStock.Add(1);
            AddParticle(new Vector3(0f, Height, -0.05f), 0.22f, ShapeRaster.Gray(242), new Vector3(0f, LaunchHeight, 0f), 0f, LaunchSeconds);
        }

        private void UpdateBar()
        {
            float fraction = Mathf.Clamp01(progress / secondsPerProbe);
            barFill.localScale = new Vector3(BarWidth * fraction, BarHeight, 1f);
            barFill.localPosition = new Vector3(-BarWidth * 0.5f + BarWidth * fraction * 0.5f, 0f, -0.01f);
        }
    }
}
