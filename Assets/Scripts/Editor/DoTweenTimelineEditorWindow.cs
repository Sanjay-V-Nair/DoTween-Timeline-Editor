using System;
using System.Collections.Generic;
using DoTweenTimeline.Runtime.Block;
using DoTweenTimeline.Runtime.SequenceRunner;
using DoTweenTimeline.Runtime.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Timeline = DoTweenTimeline.Runtime.DoTweenTimeline;

namespace DoTweenTimeline.Editor {
    /// <summary>
    /// Track-based timeline editor built with UI Toolkit and styled by Styles/TimelineWindow.uss.
    /// Pure view/controller: the component owns the data, the builder and the runner.
    /// All edits go through a SerializedObject on the component (Undo, dirty flag and prefab overrides come for free).
    /// Edit mode plays through TimelinePreview, Play mode forwards to the component.
    /// </summary>
    public class DoTweenTimelineEditorWindow : EditorWindow, ITimeline {

        // Geometry that the code relies on lives here; colours and typography live in the .uss file.
        private const float HeaderW = 112f;
        private const float RulerH = 28f;
        private const float TrackH = 40f;
        private const float MinDuration = 0.05f;
        private const float MinZoom = 20f;
        private const float MaxZoom = 600f;
        private const string StyleSheetName = "TimelineWindow.uss";

        private enum Drag { None, Scrub, Move, ResizeLeft, ResizeRight }

        private struct BlockInfo {
            public int Index;
            public float Start;
            public float Duration;
            public int Track;
            public string Label;
            public string Category;
            public float End => Start + Duration;
        }

        private class BlockView : VisualElement {
            public int Index;
            public string Category;
            public readonly Label Title = new() { pickingMode = PickingMode.Ignore };

            public BlockView() {
                AddToClassList("tl-block");
                Title.AddToClassList("tl-block__label");
                var left = new VisualElement();
                left.AddToClassList("tl-handle");
                left.AddToClassList("tl-handle--left");
                var right = new VisualElement();
                right.AddToClassList("tl-handle");
                right.AddToClassList("tl-handle--right");
                Add(Title);
                Add(left);
                Add(right);
            }
        }

        [SerializeField] private Timeline _target;
        [SerializeField] private float _pixelsPerSecond = 120f;
        [SerializeField] private float _scroll;
        [SerializeField] private int _selected = -1;
        [SerializeField] private float _cursor;
        [SerializeField] private bool _snapEnabled = true;
        [SerializeField] private float _snapStep = 0.05f;

        // Data
        private SerializedObject _so;
        private SerializedProperty _tweens;
        private VisualElement _trackerHost;
        private readonly List<BlockInfo> _infos = new();
        private readonly List<float> _tickTimes = new();
        private int _trackCount = 3;
        private int _lastBlockCount = -1;

        // Drag state
        private Drag _drag;
        private int _dragIndex;
        private float _dragPointerX;
        private float _dragStart;
        private float _dragDuration;

        // Views
        private VisualElement _toolbar;
        private VisualElement _main;
        private VisualElement _ruler;
        private VisualElement _headers;
        private VisualElement _lanes;
        private VisualElement _rowLayer;
        private VisualElement _gridLayer;
        private VisualElement _blockLayer;
        private VisualElement _playhead;
        private VisualElement _details;
        private VisualElement _detailsBody;
        private Label _detailsTitle;
        private Label _emptyState;
        private Label _timeLabel;
        private Button _playButton;
        private Slider _zoom;

        #region Open / lifecycle

        [InitializeOnLoadMethod]
        private static void LinkRuntimeEvents() {
            SequenceRunner.OnShowWarningToast -= ShowToastNotification;
            SequenceRunner.OnShowWarningToast += ShowToastNotification;
        }

        [MenuItem("Window/DoTween Timeline")]
        private static void OpenFromMenu() => Open(Selection.activeGameObject?.GetComponent<Timeline>());

        public static void Open(Timeline target) {
            var window = GetWindow<DoTweenTimelineEditorWindow>("DoTween Timeline");
            window.SetTarget(target);
            window.Show();
        }

        private static void ShowToastNotification(string msgTitle, string message) {
            // Don't open the window just to show a warning.
            if (!HasOpenInstances<DoTweenTimelineEditorWindow>()) return;
            GetWindow<DoTweenTimelineEditorWindow>().ShowNotification(new GUIContent($"{msgTitle ?? "Watch Out!"}:: {message}"), 1f);
        }

        private void OnEnable() {
            minSize = new Vector2(520f, 300f);
            TimelinePreview.Changed += OnPreviewChanged;
        }

        private void OnDisable() {
            TimelinePreview.Changed -= OnPreviewChanged;
        }

        private void OnDestroy() {
            // Closing the window must not leave previewed objects displaced.
            if (PreviewActive) TimelinePreview.Stop();
        }

        private void SetTarget(Timeline target) {
            if (PreviewActive) TimelinePreview.Stop();
            _target = target;
            _selected = -1;
            _cursor = 0f;
            if (_main != null) BindTarget();
        }

        private void OnPreviewChanged() {
            if (_main == null) return;
            UpdateTransport();
            UpdatePlayhead();
        }

        #endregion

        #region Build UI

        public void CreateGUI() {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("tl-root");
            root.focusable = true;

            var sheet = LoadStyleSheet();
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning($"DoTween Timeline: {StyleSheetName} not found next to the window script.");

            BuildToolbar(root);
            BuildMain(root);
            BuildDetails(root);

            _emptyState = new Label("Select a GameObject with a DoTweenTimeline, then open this window from its inspector or Window > DoTween Timeline.");
            _emptyState.AddToClassList("tl-empty");
            root.Add(_emptyState);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.schedule.Execute(OnTick).Every(33);

            BindTarget();
        }

        private StyleSheet LoadStyleSheet() {
            var script = MonoScript.FromScriptableObject(this);
            var scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : null;
            if (string.IsNullOrEmpty(scriptPath)) return null;
            var directory = System.IO.Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<StyleSheet>($"{directory}/Styles/{StyleSheetName}");
        }

        private void BuildToolbar(VisualElement root) {
            _toolbar = new VisualElement();
            _toolbar.AddToClassList("tl-toolbar");

            _playButton = new Button(TogglePlay) { text = "Play" };
            _playButton.AddToClassList("tl-btn");
            _playButton.AddToClassList("tl-btn--primary");

            var stop = new Button(Stop) { text = "Stop" };
            stop.AddToClassList("tl-btn");

            var add = new Button { text = "+ Add Block" };
            add.AddToClassList("tl-btn");
            add.clicked += () => ShowAddMenu(add);

            var snap = new Toggle("Snap") { value = _snapEnabled };
            snap.AddToClassList("tl-toggle");
            snap.RegisterValueChangedCallback(e => _snapEnabled = e.newValue);

            var spacer = new VisualElement();
            spacer.AddToClassList("tl-spacer");

            _timeLabel = new Label("0s / 0s");
            _timeLabel.AddToClassList("tl-time");

            _zoom = new Slider(MinZoom, MaxZoom) { value = _pixelsPerSecond };
            _zoom.AddToClassList("tl-zoom");
            _zoom.RegisterValueChangedCallback(e => SetZoom(e.newValue, _lanes.layout.width * 0.5f));

            _toolbar.Add(_playButton);
            _toolbar.Add(stop);
            _toolbar.Add(add);
            _toolbar.Add(snap);
            _toolbar.Add(spacer);
            _toolbar.Add(_timeLabel);
            _toolbar.Add(_zoom);
            root.Add(_toolbar);
        }

        private void BuildMain(VisualElement root) {
            _main = new VisualElement();
            _main.AddToClassList("tl-main");

            var rulerRow = new VisualElement();
            rulerRow.AddToClassList("tl-ruler-row");
            rulerRow.style.height = RulerH;

            var corner = new VisualElement();
            corner.AddToClassList("tl-corner");
            corner.style.width = HeaderW;

            _ruler = new VisualElement();
            _ruler.AddToClassList("tl-ruler");
            _ruler.RegisterCallback<PointerDownEvent>(OnRulerDown);
            _ruler.RegisterCallback<PointerMoveEvent>(OnRulerMove);
            _ruler.RegisterCallback<PointerUpEvent>(OnRulerUp);

            rulerRow.Add(corner);
            rulerRow.Add(_ruler);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("tl-scroll");

            var trackArea = new VisualElement();
            trackArea.AddToClassList("tl-track-area");

            _headers = new VisualElement();
            _headers.AddToClassList("tl-headers");
            _headers.style.width = HeaderW;

            _lanes = new VisualElement();
            _lanes.AddToClassList("tl-lanes");
            _lanes.RegisterCallback<GeometryChangedEvent>(_ => {
                UpdateRuler();
                UpdatePlayhead();
            });
            _lanes.RegisterCallback<PointerDownEvent>(OnLanesDown);

            _rowLayer = new VisualElement();
            _rowLayer.AddToClassList("tl-layer");
            _gridLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _gridLayer.AddToClassList("tl-layer");
            _blockLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _blockLayer.AddToClassList("tl-layer");

            _lanes.Add(_rowLayer);
            _lanes.Add(_gridLayer);
            _lanes.Add(_blockLayer);

            trackArea.Add(_headers);
            trackArea.Add(_lanes);
            scroll.Add(trackArea);

            _playhead = new VisualElement { pickingMode = PickingMode.Ignore };
            _playhead.AddToClassList("tl-playhead");
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("tl-playhead__head");
            _playhead.Add(head);

            _main.Add(rulerRow);
            _main.Add(scroll);
            _main.Add(_playhead);
            _main.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            root.Add(_main);
        }

        private void BuildDetails(VisualElement root) {
            _details = new VisualElement();
            _details.AddToClassList("tl-details");

            var header = new VisualElement();
            header.AddToClassList("tl-details__header");
            _detailsTitle = new Label("Block");
            _detailsTitle.AddToClassList("tl-details__title");
            var hint = new Label("Delete removes the block");
            hint.AddToClassList("tl-details__hint");
            header.Add(_detailsTitle);
            header.Add(hint);

            var body = new ScrollView(ScrollViewMode.Vertical);
            _detailsBody = new VisualElement();
            _detailsBody.AddToClassList("tl-details__body");
            body.Add(_detailsBody);

            _details.Add(header);
            _details.Add(body);
            root.Add(_details);
        }

        #endregion

        #region Binding

        private void BindTarget() {
            // TrackSerializedObjectValue returns void in this Unity version, so a throwaway host element owns the tracker.
            // Removing it from the hierarchy stops the old tracking.
            _trackerHost?.RemoveFromHierarchy();
            _trackerHost = null;
            _so = null;
            _tweens = null;

            if (_target != null) {
                _so = new SerializedObject(_target);
                _tweens = _so.FindProperty("tweens");
            }

            var hasTarget = _tweens != null;
            _emptyState.style.display = hasTarget ? DisplayStyle.None : DisplayStyle.Flex;
            _toolbar.style.display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
            _main.style.display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
            _details.style.display = DisplayStyle.None;
            if (!hasTarget) return;

            if (_selected >= _tweens.arraySize) _selected = -1;
            _trackerHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _trackerHost.style.position = Position.Absolute;
            _trackerHost.style.width = 0f;
            _trackerHost.style.height = 0f;
            rootVisualElement.Add(_trackerHost);
            _trackerHost.TrackSerializedObjectValue(_so, OnSerializedChanged);
            RefreshInfos();
            SyncView();
            RebuildDetails();
            UpdateTransport();
        }

        // Fires for any change to the component's data: dragging, the details panel, Undo/Redo, the inspector.
        private void OnSerializedChanged(SerializedObject so) {
            if (_tweens == null) return;
            _so.Update();
            if (_selected >= _tweens.arraySize) _selected = -1;

            RefreshInfos();
            SyncView();
            if (_lastBlockCount != _tweens.arraySize) RebuildDetails();
            if (_drag == Drag.None) CommitEdit();
        }

        // Called after any serialized change. The component invalidates its own sequence in OnValidate.
        private void CommitEdit() {
            if (PreviewActive) TimelinePreview.Rebuild();
            UpdateTransport();
            UpdatePlayhead();
        }

        private void RefreshInfos() {
            _infos.Clear();
            var maxTrack = 0;
            for (var i = 0; i < _tweens.arraySize; i++) {
                var element = _tweens.GetArrayElementAtIndex(i);
                if (element.propertyType != SerializedPropertyType.ManagedReference) continue;
                var typeName = element.managedReferenceFullTypename;
                if (string.IsNullOrEmpty(typeName)) continue;

                var nameProp = element.FindPropertyRelative("name");
                var info = new BlockInfo {
                    Index = i,
                    Start = element.FindPropertyRelative("startTime")?.floatValue ?? 0f,
                    Duration = element.FindPropertyRelative("duration")?.floatValue ?? 0f,
                    Track = Mathf.Max(0, element.FindPropertyRelative("track")?.intValue ?? 0),
                    Label = !string.IsNullOrEmpty(nameProp?.stringValue) ? nameProp.stringValue : NicifyTypeName(typeName),
                    Category = CategoryOf(typeName),
                };
                maxTrack = Mathf.Max(maxTrack, info.Track);
                _infos.Add(info);
            }
            _trackCount = Mathf.Max(3, maxTrack + 2); // always one empty track to drop blocks on
        }

        private bool TryGetInfo(int arrayIndex, out BlockInfo info) {
            foreach (var candidate in _infos) {
                if (candidate.Index != arrayIndex) continue;
                info = candidate;
                return true;
            }
            info = default;
            return false;
        }

        #endregion

        #region View sync

        private float TimeToX(float time) => time * _pixelsPerSecond - _scroll;
        private float XToTime(float x) => (x + _scroll) / _pixelsPerSecond;
        private Vector2 LanesLocal(Vector3 panelPosition) => _lanes.WorldToLocal(panelPosition);

        private static void EnsureChildren<T>(VisualElement parent, int count, Func<T> create) where T : VisualElement {
            while (parent.childCount < count) parent.Add(create());
            while (parent.childCount > count) parent.RemoveAt(parent.childCount - 1);
        }

        private void SyncView() {
            if (_lanes == null || _tweens == null) return;

            _lanes.style.height = _trackCount * TrackH;

            EnsureChildren(_headers, _trackCount, () => {
                var label = new Label();
                label.AddToClassList("tl-header");
                label.style.height = TrackH;
                return label;
            });
            EnsureChildren(_rowLayer, _trackCount, () => {
                var row = new VisualElement();
                row.AddToClassList("tl-lane");
                row.style.height = TrackH;
                return row;
            });
            for (var t = 0; t < _trackCount; t++) {
                ((Label)_headers[t]).text = $"Track {t + 1}";
                _rowLayer[t].EnableInClassList("tl-lane--alt", t % 2 == 1);
            }

            EnsureChildren(_blockLayer, _infos.Count, CreateBlockView);
            for (var i = 0; i < _infos.Count; i++) {
                var info = _infos[i];
                var view = (BlockView)_blockLayer[i];
                view.Index = info.Index;
                view.Title.text = info.Label;
                if (view.Category != info.Category) {
                    if (view.Category != null) view.RemoveFromClassList(CategoryClass(view.Category));
                    view.AddToClassList(CategoryClass(info.Category));
                    view.Category = info.Category;
                }
                view.EnableInClassList("tl-block--selected", info.Index == _selected);
                view.style.left = TimeToX(info.Start);
                view.style.width = Mathf.Max(6f, info.Duration * _pixelsPerSecond);
                view.style.top = info.Track * TrackH + 4f;
                view.style.height = TrackH - 8f;
            }

            UpdateRuler();
            UpdatePlayhead();
            UpdateTransport();
        }

        private BlockView CreateBlockView() {
            var view = new BlockView();
            view.RegisterCallback<PointerDownEvent>(e => OnBlockDown(view, e));
            view.RegisterCallback<PointerMoveEvent>(e => OnBlockMove(view, e));
            view.RegisterCallback<PointerUpEvent>(e => OnBlockUp(view, e));
            view.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            return view;
        }

        private void UpdateRuler() {
            if (_ruler == null || _lanes == null) return;
            var width = _lanes.layout.width;
            if (float.IsNaN(width) || width <= 0f) return;

            var step = PickStep();
            _tickTimes.Clear();
            for (var time = Mathf.Max(0f, Mathf.Floor(XToTime(0f) / step) * step); ; time += step) {
                if (TimeToX(time) > width) break;
                if (TimeToX(time) >= -1f) _tickTimes.Add(time);
            }

            EnsureChildren(_ruler, _tickTimes.Count, () => {
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.AddToClassList("tl-tick");
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("tl-tick__label");
                tick.Add(label);
                return tick;
            });
            EnsureChildren(_gridLayer, _tickTimes.Count, () => {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("tl-grid-line");
                return line;
            });

            for (var i = 0; i < _tickTimes.Count; i++) {
                var x = TimeToX(_tickTimes[i]);
                _ruler[i].style.left = x;
                ((Label)_ruler[i][0]).text = FormatTime(_tickTimes[i]);
                _gridLayer[i].style.left = x;
            }
        }

        private void UpdatePlayhead() {
            if (_playhead == null || _lanes == null) return;
            var width = _lanes.layout.width;
            if (float.IsNaN(width)) return;

            var x = TimeToX(CursorTime);
            var visible = x >= 0f && x <= width;
            _playhead.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _playhead.style.left = HeaderW + x - 1f;
        }

        private void UpdateTransport() {
            if (_playButton == null || _target == null) return;
            var playing = IsPlayingNow;
            _playButton.text = playing ? "Pause" : "Play";
            _playButton.EnableInClassList("tl-btn--active", playing);

            var total = 0f;
            foreach (var info in _infos) total = Mathf.Max(total, info.End);
            _timeLabel.text = $"{_target.name}   {FormatTime(CursorTime)} / {FormatTime(total)}";
        }

        private void OnTick() {
            if (_target == null || _tweens == null) return;
            UpdateTransport();
            UpdatePlayhead();
        }

        private void SetZoom(float pixelsPerSecond, float anchorX) {
            var timeUnderAnchor = XToTime(anchorX);
            _pixelsPerSecond = Mathf.Clamp(pixelsPerSecond, MinZoom, MaxZoom);
            _scroll = Mathf.Max(0f, timeUnderAnchor * _pixelsPerSecond - anchorX);
            _zoom.SetValueWithoutNotify(_pixelsPerSecond);
            SyncView();
        }

        private float PickStep() {
            float[] steps = { 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f };
            foreach (var s in steps) {
                if (s * _pixelsPerSecond >= 70f) return s;
            }
            return steps[steps.Length - 1];
        }

        #endregion

        #region Details panel

        private void SelectBlock(int arrayIndex) {
            _selected = arrayIndex;
            SyncView();
            RebuildDetails();
        }

        private void RebuildDetails() {
            _lastBlockCount = _tweens?.arraySize ?? -1;
            _detailsBody.Unbind();
            _detailsBody.Clear();

            if (_tweens == null || _selected < 0 || _selected >= _tweens.arraySize) {
                _details.style.display = DisplayStyle.None;
                return;
            }

            var element = _tweens.GetArrayElementAtIndex(_selected);
            if (string.IsNullOrEmpty(element.managedReferenceFullTypename)) {
                _details.style.display = DisplayStyle.None;
                return;
            }

            _details.style.display = DisplayStyle.Flex;
            _detailsTitle.text = NicifyTypeName(element.managedReferenceFullTypename);

            var iterator = element.Copy();
            var end = element.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end)) {
                _detailsBody.Add(new PropertyField(iterator.Copy()));
                enterChildren = false;
            }
            _detailsBody.Bind(_so);
        }

        #endregion

        #region Input

        private void OnRulerDown(PointerDownEvent e) {
            if (e.button != 0 || _target == null) return;
            _drag = Drag.Scrub;
            _ruler.CapturePointer(e.pointerId);
            SkipTo(RulerTime(e.position), false);
            rootVisualElement.Focus();
            e.StopPropagation();
        }

        private void OnRulerMove(PointerMoveEvent e) {
            if (_drag != Drag.Scrub || !_ruler.HasPointerCapture(e.pointerId)) return;
            SkipTo(RulerTime(e.position), false);
            e.StopPropagation();
        }

        private void OnRulerUp(PointerUpEvent e) {
            if (!_ruler.HasPointerCapture(e.pointerId)) return;
            _ruler.ReleasePointer(e.pointerId);
            _drag = Drag.None;
            e.StopPropagation();
        }

        private float RulerTime(Vector3 panelPosition) => XToTime(_ruler.WorldToLocal(panelPosition).x);

        private void OnLanesDown(PointerDownEvent e) {
            if (e.button != 0) return;
            rootVisualElement.Focus();
            if (_selected != -1) SelectBlock(-1);
        }

        private void OnBlockDown(BlockView view, PointerDownEvent e) {
            if (e.button != 0 || !TryGetInfo(view.Index, out var info)) return;

            var target = e.target as VisualElement;
            _drag = target != null && target.ClassListContains("tl-handle--left") ? Drag.ResizeLeft
                  : target != null && target.ClassListContains("tl-handle--right") ? Drag.ResizeRight
                  : Drag.Move;
            _dragIndex = view.Index;
            _dragPointerX = LanesLocal(e.position).x;
            _dragStart = info.Start;
            _dragDuration = info.Duration;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Edit Timeline Block");
            SelectBlock(view.Index);
            view.CapturePointer(e.pointerId);
            rootVisualElement.Focus();
            e.StopPropagation();
        }

        private void OnBlockMove(BlockView view, PointerMoveEvent e) {
            if (_drag == Drag.None || _drag == Drag.Scrub || !view.HasPointerCapture(e.pointerId)) return;
            ApplyDrag(LanesLocal(e.position));
            e.StopPropagation();
        }

        private void OnBlockUp(BlockView view, PointerUpEvent e) {
            if (!view.HasPointerCapture(e.pointerId)) return;
            view.ReleasePointer(e.pointerId);
            EndDrag();
            e.StopPropagation();
        }

        private void EndDrag() {
            var wasEdit = _drag != Drag.None && _drag != Drag.Scrub;
            _drag = Drag.None;
            if (wasEdit) CommitEdit();
        }

        private void ApplyDrag(Vector2 lanesPosition) {
            if (_dragIndex < 0 || _dragIndex >= _tweens.arraySize) return;

            var element = _tweens.GetArrayElementAtIndex(_dragIndex);
            var delta = (lanesPosition.x - _dragPointerX) / _pixelsPerSecond;
            var end = _dragStart + _dragDuration;

            switch (_drag) {
                case Drag.Move:
                    SetFloat(element, "startTime", Mathf.Max(0f, Snap(_dragStart + delta)));
                    SetInt(element, "track", Mathf.Clamp(Mathf.FloorToInt(lanesPosition.y / TrackH), 0, _trackCount - 1));
                    break;
                case Drag.ResizeRight:
                    SetFloat(element, "duration", Mathf.Max(MinDuration, Snap(_dragDuration + delta)));
                    break;
                case Drag.ResizeLeft:
                    var newStart = Mathf.Clamp(Snap(_dragStart + delta), 0f, end - MinDuration);
                    SetFloat(element, "startTime", newStart);
                    SetFloat(element, "duration", end - newStart);
                    break;
            }

            _so.ApplyModifiedProperties();
            RefreshInfos();
            SyncView();
        }

        private void OnWheel(WheelEvent e) {
            if (_lanes == null) return;
            if (e.ctrlKey || e.commandKey) {
                var anchor = _lanes.WorldToLocal(e.originalMousePosition).x;
                SetZoom(_pixelsPerSecond * (1f - e.delta.y * 0.05f), anchor);
                e.StopPropagation();
            }
            else if (e.shiftKey || Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y)) {
                var amount = e.shiftKey ? e.delta.y : e.delta.x;
                _scroll = Mathf.Max(0f, _scroll + amount * 20f);
                SyncView();
                e.StopPropagation();
            }
        }

        private void OnKeyDown(KeyDownEvent e) {
            if (_target == null || e.target is TextElement) return; // let text fields keep their keys

            switch (e.keyCode) {
                case KeyCode.Space:
                    TogglePlay();
                    e.StopPropagation();
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    if (_selected < 0) return;
                    DeleteSelected();
                    e.StopPropagation();
                    break;
            }
        }

        private float Snap(float time) => _snapEnabled && _snapStep > 0f ? Mathf.Round(time / _snapStep) * _snapStep : time;

        private static void SetFloat(SerializedProperty element, string field, float value) {
            var p = element.FindPropertyRelative(field);
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedProperty element, string field, int value) {
            var p = element.FindPropertyRelative(field);
            if (p != null) p.intValue = value;
        }

        #endregion

        #region Editing

        private void ShowAddMenu(VisualElement anchor) {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<TweenBlock>()) {
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null) continue;
                var blockType = type;
                menu.AddItem(new GUIContent($"{CategoryOf(type.FullName)}/{NicifyTypeName(type.Name)}"), false, () => AddBlock(blockType));
            }
            menu.DropDown(anchor.worldBound);
        }

        private void AddBlock(Type type) {
            if (_so == null) return;
            _so.Update();

            var block = (TweenBlock)Activator.CreateInstance(type);
            block.startTime = Snap(CursorTime);
            block.duration = 1f;
            block.gameObject = _target.gameObject; // sensible default, avoids null targets
            block.track = _selected >= 0 && _selected < _tweens.arraySize
                ? _tweens.GetArrayElementAtIndex(_selected).FindPropertyRelative("track")?.intValue ?? 0
                : 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add Timeline Block");
            var index = _tweens.arraySize;
            _tweens.InsertArrayElementAtIndex(index);
            _tweens.GetArrayElementAtIndex(index).managedReferenceValue = block;
            _so.ApplyModifiedProperties();

            _selected = index;
            RefreshInfos();
            SyncView();
            RebuildDetails();
            CommitEdit();
        }

        private void DeleteSelected() {
            if (_so == null || _selected < 0 || _selected >= _tweens.arraySize) return;
            var before = _tweens.arraySize;
            _tweens.DeleteArrayElementAtIndex(_selected);
            if (_tweens.arraySize == before) _tweens.DeleteArrayElementAtIndex(_selected); // first call only nulls the reference
            _so.ApplyModifiedProperties();

            _selected = -1;
            RefreshInfos();
            SyncView();
            RebuildDetails();
            CommitEdit();
        }

        #endregion

        #region ITimeline (forwards to the component in Play mode, to TimelinePreview in edit mode)

        public void Play() {
            if (_target == null) return;
            if (Application.isPlaying) {
                if (!HasBlocks()) return;
                _target.Play();
                return;
            }
            if (EnsurePreview()) TimelinePreview.Play();
        }

        public void Pause() {
            if (_target == null) return;
            if (Application.isPlaying) _target.Pause();
            else TimelinePreview.Pause();
        }

        public void Resume() {
            if (_target == null) return;
            if (Application.isPlaying) _target.Resume();
            else Play();
        }

        public void SkipTo(float t, bool isPlay = true) {
            if (_target == null) return;
            _cursor = Mathf.Max(0f, t);
            if (Application.isPlaying) {
                if (HasBlocks()) _target.SkipTo(_cursor, isPlay);
                return;
            }
            if (!EnsurePreview()) {
                UpdatePlayhead();
                return;
            }
            TimelinePreview.Scrub(_cursor);
            if (isPlay) TimelinePreview.Play();
            else TimelinePreview.Pause();
        }

        public void Stop() {
            if (_target == null) return;
            _cursor = 0f;
            if (Application.isPlaying) {
                if (_target.Blocks == null || _target.Blocks.Count == 0) return;
                _target.Pause();
                _target.SkipTo(0f, false);
            }
            else {
                TimelinePreview.Stop();
            }
            UpdateTransport();
            UpdatePlayhead();
        }

        private void TogglePlay() {
            if (IsPlayingNow) Pause();
            else Play();
            UpdateTransport();
        }

        private bool PreviewActive => TimelinePreview.IsActive && TimelinePreview.Target == _target;

        private bool IsPlayingNow => Application.isPlaying ? _target != null && _target.IsPlaying : PreviewActive && TimelinePreview.IsPlaying;

        private float CursorTime {
            get {
                if (Application.isPlaying) return _target != null ? _target.PlaybackTime : 0f;
                return PreviewActive ? TimelinePreview.Time : _cursor;
            }
        }

        private bool EnsurePreview() {
            if (Application.isPlaying || _target == null) return false;
            if (PreviewActive) return true;
            if (!TimelinePreview.Begin(_target)) {
                NotifyNoBlocks();
                return false;
            }
            TimelinePreview.Scrub(_cursor);
            return true;
        }

        private bool HasBlocks() {
            if (_target.Blocks != null && _target.Blocks.Count > 0) return true;
            NotifyNoBlocks();
            return false;
        }

        private void NotifyNoBlocks() => ShowNotification(new GUIContent("Add Tween Blocks to Get Tweenified!"), 1.5f);

        #endregion

        #region Helpers

        private static string FormatTime(float t) => t.ToString("0.##") + "s";

        private static string NicifyTypeName(string fullOrShortTypeName) {
            var typeName = fullOrShortTypeName;
            var space = typeName.IndexOf(' ');
            if (space >= 0) typeName = typeName.Substring(space + 1); // drop "Assembly " prefix of managedReferenceFullTypename
            var name = typeName.Substring(typeName.LastIndexOfAny(new[] { '.', '/' }) + 1);
            name = name.Replace("DoTween", string.Empty).Replace("Block", string.Empty);
            return string.IsNullOrEmpty(name) ? typeName : ObjectNames.NicifyVariableName(name);
        }

        // Blocks are grouped by the last namespace segment, e.g. ...Block.Transform -> "Transform".
        private static string CategoryOf(string fullTypename) {
            if (string.IsNullOrEmpty(fullTypename)) return "Other";
            var space = fullTypename.IndexOf(' ');
            var typeName = space >= 0 ? fullTypename.Substring(space + 1) : fullTypename;
            var lastDot = typeName.LastIndexOf('.');
            if (lastDot < 0) return "Other";
            var ns = typeName.Substring(0, lastDot);
            return ns.Substring(ns.LastIndexOf('.') + 1);
        }

        private static string CategoryClass(string category) => "tl-cat-" + category.ToLowerInvariant();

        #endregion
    }
}
