using System;
using System.Collections.Generic;
using DoTweenTimeline.Runtime.Block;
using DoTweenTimeline.Runtime.SequenceRunner;
using DoTweenTimeline.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using Timeline = DoTweenTimeline.Runtime.DoTweenTimeline;

namespace DoTweenTimeline.Editor {
    /// <summary>
    /// Track-based timeline editor. Pure view/controller: the component owns the data, the builder and the runner.
    /// All edits go through a SerializedObject on the component (Undo, dirty flag and prefab overrides come for free).
    /// Edit mode plays through TimelinePreview, Play mode forwards to the component.
    /// </summary>
    public class DoTweenTimelineEditorWindow : EditorWindow, ITimeline {

        private const float ToolbarH = 22f;
        private const float RulerH = 22f;
        private const float HeaderW = 110f;
        private const float TrackH = 34f;
        private const float DetailsH = 190f;
        private const float EdgeW = 6f;
        private const float MinDuration = 0.05f;

        private enum Drag { None, Scrub, Move, ResizeLeft, ResizeRight }

        private struct BlockInfo {
            public int Index;
            public float Start;
            public float Duration;
            public int Track;
            public string Label;
            public Color Color;
            public float End => Start + Duration;
        }

        [SerializeField] private Timeline _target;
        [SerializeField] private float _pixelsPerSecond = 120f;
        [SerializeField] private float _scroll;
        [SerializeField] private int _selected = -1;
        [SerializeField] private float _cursor;
        [SerializeField] private bool _snapEnabled = true;
        [SerializeField] private float _snapStep = 0.05f;
        [SerializeField] private Vector2 _detailsScroll;

        private SerializedObject _so;
        private SerializedProperty _tweens;
        private readonly List<BlockInfo> _infos = new();
        private int _trackCount = 3;

        private Drag _drag;
        private int _dragIndex;
        private float _dragMouseX;
        private float _dragStart;
        private float _dragDuration;

        private Rect _rulerRect;
        private Rect _tracksRect;
        private Rect _detailsRect;
        private GUIStyle _blockLabelStyle;
        private GUIStyle _labelStyle;

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
            minSize = new Vector2(480f, 260f);
            TimelinePreview.Changed += Repaint;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable() {
            TimelinePreview.Changed -= Repaint;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnDestroy() {
            // Closing the window must not leave previewed objects displaced.
            if (PreviewActive) TimelinePreview.Stop();
        }

        private void Update() {
            if (Application.isPlaying && _target != null) Repaint();
        }

        private void SetTarget(Timeline target) {
            if (PreviewActive) TimelinePreview.Stop();
            _target = target;
            _so = null;
            _tweens = null;
            _selected = -1;
            _cursor = 0f;
            Repaint();
        }

        private void OnUndoRedo() {
            _so?.Update();
            if (_selected >= (_tweens?.arraySize ?? 0)) _selected = -1;
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
            if (!EnsurePreview()) return;
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

        // Called after any serialized change. Component invalidates its own sequence in OnValidate.
        private void CommitEdit() {
            if (PreviewActive) TimelinePreview.Rebuild();
            Repaint();
        }

        #endregion

        #region OnGUI

        private void OnGUI() {
            if (!Bind()) {
                EditorGUILayout.HelpBox("Select a GameObject with a DoTweenTimeline, then open this window from its inspector or Window > DoTween Timeline.", MessageType.Info);
                return;
            }

            ComputeLayout();
            RefreshInfos();
            HandleInput(Event.current);

            if (Event.current.type == EventType.Repaint) DrawTimeline();
            DrawToolbar();
            DrawDetails();
        }

        private bool Bind() {
            if (_target == null) {
                _so = null;
                _tweens = null;
                return false;
            }
            if (_so == null || _so.targetObject != _target) {
                _so = new SerializedObject(_target);
                _tweens = _so.FindProperty("tweens");
            }
            if (_tweens == null) return false;
            _so.Update();
            return true;
        }

        private void ComputeLayout() {
            var hasSelection = _selected >= 0 && _selected < _tweens.arraySize;
            var detailsH = hasSelection ? DetailsH : 0f;
            var top = ToolbarH;
            _rulerRect = new Rect(HeaderW, top, position.width - HeaderW, RulerH);
            _tracksRect = new Rect(HeaderW, top + RulerH, position.width - HeaderW, Mathf.Max(0f, position.height - top - RulerH - detailsH));
            _detailsRect = new Rect(0f, position.height - detailsH, position.width, detailsH);
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
                    Color = ColorFor(typeName),
                };
                maxTrack = Mathf.Max(maxTrack, info.Track);
                _infos.Add(info);
            }
            _trackCount = Mathf.Max(3, maxTrack + 2); // always one empty track to drop blocks on
        }

        #endregion

        #region Coordinates

        private float TimeToX(float time) => _tracksRect.x + time * _pixelsPerSecond - _scroll;
        private float XToTime(float x) => (x - _tracksRect.x + _scroll) / _pixelsPerSecond;
        private float TrackY(int track) => _tracksRect.y + track * TrackH;

        private Rect BlockRect(BlockInfo info) {
            var x = TimeToX(info.Start);
            var width = Mathf.Max(4f, info.Duration * _pixelsPerSecond);
            return new Rect(x, TrackY(info.Track) + 3f, width, TrackH - 6f);
        }

        private float Snap(float time) => _snapEnabled && _snapStep > 0f ? Mathf.Round(time / _snapStep) * _snapStep : time;

        #endregion

        #region Input

        private void HandleInput(Event e) {
            var mouse = e.mousePosition;

            switch (e.type) {
                case EventType.MouseDown when e.button == 0:
                    if (_rulerRect.Contains(mouse)) {
                        _drag = Drag.Scrub;
                        SkipTo(XToTime(mouse.x), false);
                        e.Use();
                    }
                    else if (_tracksRect.Contains(mouse)) {
                        BeginBlockDrag(mouse);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag when _drag != Drag.None:
                    ApplyDrag(mouse);
                    e.Use();
                    break;

                case EventType.MouseUp when _drag != Drag.None:
                    var wasEdit = _drag != Drag.Scrub;
                    _drag = Drag.None;
                    if (wasEdit) CommitEdit();
                    e.Use();
                    break;

                case EventType.ScrollWheel when _rulerRect.Contains(mouse) || _tracksRect.Contains(mouse):
                    if (e.control || e.command) {
                        var timeUnderMouse = XToTime(mouse.x);
                        _pixelsPerSecond = Mathf.Clamp(_pixelsPerSecond * (1f - e.delta.y * 0.05f), 20f, 600f);
                        _scroll = Mathf.Max(0f, timeUnderMouse * _pixelsPerSecond - (mouse.x - _tracksRect.x));
                    }
                    else {
                        _scroll = Mathf.Max(0f, _scroll + (e.delta.x + e.delta.y) * 20f);
                    }
                    e.Use();
                    Repaint();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace:
                    if (_selected >= 0 && !EditorGUIUtility.editingTextField) {
                        DeleteSelected();
                        e.Use();
                    }
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.Space && !EditorGUIUtility.editingTextField:
                    if (IsPlayingNow) Pause(); else Play();
                    e.Use();
                    break;
            }
        }

        private void BeginBlockDrag(Vector2 mouse) {
            // Topmost (last drawn) block wins.
            for (var i = _infos.Count - 1; i >= 0; i--) {
                var rect = BlockRect(_infos[i]);
                if (!rect.Contains(mouse)) continue;

                var edge = Mathf.Min(EdgeW, rect.width / 3f);
                _drag = mouse.x <= rect.x + edge ? Drag.ResizeLeft
                      : mouse.x >= rect.xMax - edge ? Drag.ResizeRight
                      : Drag.Move;
                _dragIndex = _infos[i].Index;
                _dragMouseX = mouse.x;
                _dragStart = _infos[i].Start;
                _dragDuration = _infos[i].Duration;
                _selected = _dragIndex;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Edit Timeline Block");
                return;
            }
            _selected = -1;
        }

        private void ApplyDrag(Vector2 mouse) {
            if (_drag == Drag.Scrub) {
                SkipTo(XToTime(mouse.x), false);
                return;
            }
            if (_dragIndex < 0 || _dragIndex >= _tweens.arraySize) return;

            var element = _tweens.GetArrayElementAtIndex(_dragIndex);
            var delta = (mouse.x - _dragMouseX) / _pixelsPerSecond;
            var end = _dragStart + _dragDuration;

            switch (_drag) {
                case Drag.Move:
                    SetFloat(element, "startTime", Mathf.Max(0f, Snap(_dragStart + delta)));
                    var track = Mathf.Clamp(Mathf.FloorToInt((mouse.y - _tracksRect.y) / TrackH), 0, _trackCount - 1);
                    SetInt(element, "track", track);
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
            Repaint();
        }

        private void DeleteSelected() {
            if (_selected < 0 || _selected >= _tweens.arraySize) return;
            var before = _tweens.arraySize;
            _tweens.DeleteArrayElementAtIndex(_selected);
            if (_tweens.arraySize == before) _tweens.DeleteArrayElementAtIndex(_selected); // first call only nulls the reference
            _so.ApplyModifiedProperties();
            _selected = -1;
            CommitEdit();
        }

        private void ShowAddMenu() {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<TweenBlock>()) {
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null) continue;
                var blockType = type;
                menu.AddItem(new GUIContent(NicifyTypeName(type.Name)), false, () => AddBlock(blockType));
            }
            menu.ShowAsContext();
        }

        private void AddBlock(Type type) {
            if (!Bind()) return;
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
            CommitEdit();
        }

        private static void SetFloat(SerializedProperty element, string field, float value) {
            var p = element.FindPropertyRelative(field);
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedProperty element, string field, int value) {
            var p = element.FindPropertyRelative(field);
            if (p != null) p.intValue = value;
        }

        #endregion

        #region Drawing

        private static Color Pick(Color dark, Color light) => EditorGUIUtility.isProSkin ? dark : light;

        private void DrawTimeline() {
            var bg = Pick(new Color(0.16f, 0.16f, 0.16f), new Color(0.78f, 0.78f, 0.78f));
            var rowA = Pick(new Color(0.19f, 0.19f, 0.19f), new Color(0.82f, 0.82f, 0.82f));
            var rowB = Pick(new Color(0.21f, 0.21f, 0.21f), new Color(0.85f, 0.85f, 0.85f));
            var panel = Pick(new Color(0.23f, 0.23f, 0.23f), new Color(0.72f, 0.72f, 0.72f));
            var grid = Pick(new Color(1f, 1f, 1f, 0.07f), new Color(0f, 0f, 0f, 0.10f));
            var text = Pick(new Color(0.8f, 0.8f, 0.8f), new Color(0.15f, 0.15f, 0.15f));

            EditorGUI.DrawRect(new Rect(0f, ToolbarH, position.width, position.height - ToolbarH), bg);

            // Track rows
            for (var t = 0; t < _trackCount; t++) {
                var y = TrackY(t);
                if (y >= _tracksRect.yMax) break;
                EditorGUI.DrawRect(new Rect(_tracksRect.x, y, _tracksRect.width, Mathf.Min(TrackH, _tracksRect.yMax - y)), t % 2 == 0 ? rowA : rowB);
            }

            // Grid + ruler ticks
            var step = PickStep();
            var first = Mathf.Floor(XToTime(_rulerRect.x) / step) * step;
            _labelStyle ??= new GUIStyle(EditorStyles.miniLabel);
            _labelStyle.normal.textColor = text;
            var labelStyle = _labelStyle;
            for (var time = Mathf.Max(0f, first); TimeToX(time) < _rulerRect.xMax; time += step) {
                var x = TimeToX(time);
                if (x < _rulerRect.x) continue;
                EditorGUI.DrawRect(new Rect(x, _tracksRect.y, 1f, _tracksRect.height), grid);
            }

            // Blocks
            _blockLabelStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 4, 0, 0) };
            _blockLabelStyle.normal.textColor = Color.black;
            foreach (var info in _infos) {
                var rect = BlockRect(info);
                if (rect.xMax < _tracksRect.x || rect.x > _tracksRect.xMax || rect.y >= _tracksRect.yMax) continue;
                DrawBlock(rect, info, info.Index == _selected);
            }

            // Ruler and headers sit on top so scrolled content never bleeds over them
            EditorGUI.DrawRect(new Rect(0f, ToolbarH, position.width, RulerH), panel);
            EditorGUI.DrawRect(new Rect(0f, _tracksRect.y, HeaderW, _tracksRect.height), panel);
            EditorGUI.DrawRect(new Rect(0f, ToolbarH + RulerH - 1f, position.width, 1f), grid);

            for (var time = Mathf.Max(0f, first); TimeToX(time) < _rulerRect.xMax; time += step) {
                var x = TimeToX(time);
                if (x < _rulerRect.x) continue;
                EditorGUI.DrawRect(new Rect(x, _rulerRect.y + RulerH - 10f, 1f, 10f), text);
                GUI.Label(new Rect(x + 3f, _rulerRect.y + 2f, 60f, 14f), FormatTime(time), labelStyle);
            }

            for (var t = 0; t < _trackCount; t++) {
                var y = TrackY(t);
                if (y >= _tracksRect.yMax) break;
                GUI.Label(new Rect(8f, y + 8f, HeaderW - 12f, 18f), $"Track {t + 1}", labelStyle);
            }

            DrawPlayhead();
        }

        private void DrawBlock(Rect rect, BlockInfo info, bool selected) {
            EditorGUI.DrawRect(rect, info.Color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, EdgeW / 2f, rect.height), new Color(0f, 0f, 0f, 0.25f));
            EditorGUI.DrawRect(new Rect(rect.xMax - EdgeW / 2f, rect.y, EdgeW / 2f, rect.height), new Color(0f, 0f, 0f, 0.25f));
            if (selected) DrawOutline(rect, Color.white, 2f);
            GUI.Label(rect, info.Label, _blockLabelStyle);

            var edge = Mathf.Min(EdgeW, rect.width / 3f);
            EditorGUIUtility.AddCursorRect(new Rect(rect.x, rect.y, edge, rect.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(rect.xMax - edge, rect.y, edge, rect.height), MouseCursor.ResizeHorizontal);
        }

        private static void DrawOutline(Rect r, Color c, float w) {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        private void DrawPlayhead() {
            var x = TimeToX(CursorTime);
            if (x < _tracksRect.x || x > _tracksRect.xMax) return;
            var color = new Color(0.95f, 0.25f, 0.25f);
            EditorGUI.DrawRect(new Rect(x - 1f, _rulerRect.y, 2f, _rulerRect.height + _tracksRect.height), color);
            EditorGUI.DrawRect(new Rect(x - 5f, _rulerRect.y, 11f, 7f), color);
        }

        private float PickStep() {
            float[] steps = { 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f };
            foreach (var s in steps) {
                if (s * _pixelsPerSecond >= 70f) return s;
            }
            return steps[^1];
        }

        private static string FormatTime(float t) => t.ToString("0.##") + "s";

        private void DrawToolbar() {
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, ToolbarH));
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                if (GUILayout.Button(IsPlayingNow ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(50f))) {
                    if (IsPlayingNow) Pause(); else Play();
                }
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(44f))) Stop();

                GUILayout.Space(8f);
                if (GUILayout.Button("+ Add Block", EditorStyles.toolbarDropDown, GUILayout.Width(90f))) ShowAddMenu();
                _snapEnabled = GUILayout.Toggle(_snapEnabled, "Snap", EditorStyles.toolbarButton, GUILayout.Width(42f));

                GUILayout.FlexibleSpace();

                var total = 0f;
                foreach (var info in _infos) total = Mathf.Max(total, info.End);
                GUILayout.Label($"{_target.name}   {FormatTime(CursorTime)} / {FormatTime(total)}", EditorStyles.miniLabel);
                GUILayout.Space(8f);
                _pixelsPerSecond = GUILayout.HorizontalSlider(_pixelsPerSecond, 20f, 600f, GUILayout.Width(90f));
            }
            GUILayout.EndArea();
        }

        private void DrawDetails() {
            if (_selected < 0 || _selected >= _tweens.arraySize || _detailsRect.height <= 0f) return;

            GUILayout.BeginArea(_detailsRect, EditorStyles.helpBox);
            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            EditorGUILayout.LabelField("Block Details", EditorStyles.boldLabel);
            BlockInspector.Draw(_tweens.GetArrayElementAtIndex(_selected));
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            if (_so.ApplyModifiedProperties()) CommitEdit();
        }

        #endregion

        #region Helpers

        private static string NicifyTypeName(string fullOrShortTypeName) {
            var name = fullOrShortTypeName.Substring(fullOrShortTypeName.LastIndexOfAny(new[] { '.', '/' }) + 1);
            name = name.Replace("DoTween", string.Empty).Replace("Block", string.Empty);
            return string.IsNullOrEmpty(name) ? fullOrShortTypeName : ObjectNames.NicifyVariableName(name);
        }

        private static Color ColorFor(string typeName) {
            var hash = 17;
            foreach (var c in typeName) hash = unchecked(hash * 31 + c);
            var hue = (Mathf.Abs(hash) % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.78f);
        }

        #endregion
    }
}
