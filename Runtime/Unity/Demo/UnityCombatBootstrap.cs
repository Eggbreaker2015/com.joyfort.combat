using System;
using Combat.Core.Battle;
using Combat.Foundation.Diagnostics;
using Combat.Runtime.Display;
using Combat.Runtime.Runner;
using Combat.Unity.Authoring;
using Combat.Unity.Diagnostics;
using Combat.Unity.Display;
using UnityEngine;

namespace Combat.Unity.Demo
{
    public sealed class UnityCombatBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleScenarioAsset _scenario;
        [SerializeField] private CombatLogSettingsAsset _logSettings;
        [SerializeField] private UnityUnitFacingMode _unitFacingMode = UnityUnitFacingMode.SideScrollerFlip;
        [SerializeField] private CombatantPresentationCatalogAsset _combatantPresentationCatalog;
        [SerializeField] private SpriteAnimationSetAsset _projectileAnimationSet;
        [SerializeField] private SpriteAnimationSetAsset _feedbackAnimationSet;
        [SerializeField] private bool _enableRuntimeObserver;

        private GameObject _rootObject;
        private Transform _rootTransform;
        private BattleInstance _instance;
        private BattlePresentationBridge _presentation;

        private void Start()
        {
            BuildAuthoringPreview();
        }

        private void BuildAuthoringPreview()
        {
            if (_scenario == null)
            {
                throw new InvalidOperationException("UnityCombatBootstrap requires a BattleScenarioAsset before Start.");
            }

            BuildAuthoringPreview(
                () => UnityCombatLogFactory.Create(_logSettings, this),
                (parent, logger) => new UnityCombatViewPort(
                    parent,
                    logger,
                    null,
                    _unitFacingMode,
                    _combatantPresentationCatalog,
                    _projectileAnimationSet,
                    _feedbackAnimationSet,
                    enableRuntimeObserver: _enableRuntimeObserver),
                (viewPort, instance) =>
                    ((UnityCombatViewPort)viewPort).SetRuntimeSnapshotSource(instance.Simulation),
                destroyRoot: null);
        }

        internal void BuildAuthoringPreview(
            Func<CombatLogger> createLogger,
            Func<Transform, CombatLogger, ICombatViewPort> createViewPort,
            Action<ICombatViewPort, BattleInstance> bindSnapshotSource,
            Action<GameObject> destroyRoot)
        {
            if (_scenario == null)
            {
                throw new InvalidOperationException(
                    "UnityCombatBootstrap requires a BattleScenarioAsset before preview composition.");
            }

            if (createLogger == null)
            {
                throw new ArgumentNullException(nameof(createLogger));
            }

            if (createViewPort == null)
            {
                throw new ArgumentNullException(nameof(createViewPort));
            }

            DestroyViewRoot();
            GameObject candidateRoot = null;
            try
            {
                candidateRoot = new GameObject("CombatViewRoot");
                candidateRoot.SetActive(false);
                CombatLogger logger = createLogger() ?? CombatLogger.Disabled;
                ICombatViewPort viewPort = createViewPort(candidateRoot.transform, logger)
                    ?? throw new InvalidOperationException(
                        "Combat authoring preview viewport factory returned null.");
                BattleConfig config = BattleAuthoringConverter.BuildBattleConfig(_scenario);
                BattleInitialPresentationComposition composition =
                    BattleInstance.CreateForPresentation(config, logger);
                var presentation = new BattlePresentationBridge(
                    new VisualPresentationScheduler(new ImmediateVisualCommandSink(viewPort)));

                presentation.Consume(composition.InitialOutput.Events);
                BattleInstance instance = composition.CompletePresentation();
                bindSnapshotSource?.Invoke(viewPort, instance);

                candidateRoot.SetActive(true);
                _rootObject = candidateRoot;
                _rootTransform = candidateRoot.transform;
                _presentation = presentation;
                _instance = instance;
                candidateRoot = null;
            }
            catch
            {
                DestroyRootSynchronously(candidateRoot, destroyRoot);
                throw;
            }
        }

        private void OnDestroy()
        {
            DestroyViewRoot();
            _presentation = null;
            _instance = null;
        }

        private void DestroyViewRoot()
        {
            GameObject root = _rootObject;
            _presentation = null;
            _instance = null;
            _rootTransform = null;
            _rootObject = null;
            DestroyRootSynchronously(root, destroyRoot: null);
        }

        private static void DestroyRootSynchronously(
            GameObject root,
            Action<GameObject> destroyRoot)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            root.transform.SetParent(null, false);
            if (destroyRoot != null)
            {
                destroyRoot(root);
            }
            else if (Application.isPlaying)
            {
                Destroy(root);
            }
            else
            {
                DestroyImmediate(root);
            }
        }
    }
}
