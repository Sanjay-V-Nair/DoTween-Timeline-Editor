// Made with 💖 by Sanjay V Nair
using UnityEngine;
using UnityEditor;
using Hitwicket.DoTweenTimeline;
using System.Collections.Generic;
using DG.Tweening;

namespace Hitwicket.Editor.DoTweenTimeline
{
    public class DoTweenTimelineWindow : EditorWindow
    {
        private Hitwicket.DoTweenTimeline.DoTweenTimeline targetTimeline;
        
        private float zoomScale = 100f; // pixels per second
        private Vector2 scrollPosition;
        
        private float playheadTime = 0f;
        private Sequence previewSequence;
        private bool isPlaying = false;
        private double lastUpdateTime;

        private const float headerHeight = 30f;
        private const float leftPanelWidth = 250f;
        private const float baseTrackHeight = 40f;
        private const float expandedPropertyHeight = 180f; // Increased space for properties to prevent overlap
        
        private int draggingBlockIndex = -1;
        private int selectedBlockIndex = -1;
        private enum DragType { None, Center, LeftEdge, RightEdge }
        private DragType currentDragType = DragType.None;
        private bool isDraggingPlayhead = false;
        
        private float initialDragMouseX = 0f;
        private float initialDragStartTime = 0f;
        private float initialDragDuration = 0f;

        [MenuItem("Window/Hitwicket/DOTween Timeline")]
        public static void ShowWindowFromMenu()
        {
            GetWindow<DoTweenTimelineWindow>("DOTween Timeline");
        }

        public static void ShowWindow(Hitwicket.DoTweenTimeline.DoTweenTimeline timeline)
        {
            var window = GetWindow<DoTweenTimelineWindow>("DOTween Timeline");
            window.targetTimeline = timeline;
            window.GeneratePreviewSequence();
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (previewSequence != null)
            {
                previewSequence.Kill();
                previewSequence = null;
            }
        }

        private void OnEditorUpdate()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - lastUpdateTime);
            lastUpdateTime = currentTime;
            
            if (isPlaying)
            {
                playheadTime += deltaTime;
                if (playheadTime > targetTimeline.timelineDuration)
                {
                    playheadTime = targetTimeline.timelineDuration;
                    isPlaying = false;
                }
                UpdatePreview();
                Repaint();
            }
        }

        private void GeneratePreviewSequence()
        {
            if (previewSequence != null) 
            {
                previewSequence.Rewind();
                previewSequence.Kill();
            }
            if (targetTimeline == null) return;
            
            previewSequence = targetTimeline.GenerateSequence();
            previewSequence.Pause();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (previewSequence == null) GeneratePreviewSequence();
            if (previewSequence != null && previewSequence.IsActive())
            {
                previewSequence.Goto(playheadTime, true);
            }
        }

        private void OnGUI()
        {
            if (targetTimeline == null)
            {
                EditorGUILayout.HelpBox("Select a DoTweenTimeline component and click 'Open Timeline Editor' to begin.", UnityEditor.MessageType.Info);
                return;
            }

            DrawHeader();
            DrawTimelineArea();
            HandleEvents();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(headerHeight));
            
            if (GUILayout.Button("↻ Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                GeneratePreviewSequence();
            }
            
            string playIcon = isPlaying ? "❚❚" : "►";
            if (GUILayout.Button(playIcon, EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                if (isPlaying)
                {
                    isPlaying = false;
                }
                else 
                {
                    if (playheadTime >= targetTimeline.timelineDuration)
                        playheadTime = 0f;
                    isPlaying = true;
                    if (previewSequence == null) GeneratePreviewSequence();
                }
            }
            
            GUILayout.Space(10);
            Undo.RecordObject(targetTimeline, "Timeline Duration Change");
            targetTimeline.timelineDuration = EditorGUILayout.FloatField("Duration (s)", targetTimeline.timelineDuration, GUILayout.Width(150));
            zoomScale = EditorGUILayout.Slider("Zoom", zoomScale, 10f, 500f, GUILayout.Width(200));
            
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Playhead: {playheadTime:F2}s");
            
            GUILayout.EndHorizontal();
        }

        private void DrawTimelineArea()
        {
            if (targetTimeline.blocks == null) targetTimeline.blocks = new List<DoTweenBlock>();

            float totalHeight = 0f;
            for (int i = 0; i < targetTimeline.blocks.Count; i++)
            {
                var block = targetTimeline.blocks[i];
                if (block == null) continue;
                totalHeight += baseTrackHeight + (block.isExpanded ? expandedPropertyHeight : 0);
            }
            
            float timelineContentWidth = targetTimeline.timelineDuration * zoomScale;

            scrollPosition = GUI.BeginScrollView(
                new Rect(0, headerHeight, position.width, position.height - headerHeight), 
                scrollPosition, 
                new Rect(0, 0, timelineContentWidth + leftPanelWidth, Mathf.Max(totalHeight, position.height - headerHeight)));

            // Background Grid
            if (Event.current.type == EventType.Repaint)
            {
                for (int i = 0; i <= Mathf.CeilToInt(targetTimeline.timelineDuration); i++)
                {
                    float xPos = leftPanelWidth + i * zoomScale;
                    EditorGUI.DrawRect(new Rect(xPos, 0, 1, Mathf.Max(totalHeight, position.height)), new Color(0.5f, 0.5f, 0.5f, 0.2f));
                }
            }

            float currentY = 0f;
            for (int i = 0; i < targetTimeline.blocks.Count; i++)
            {
                var block = targetTimeline.blocks[i];
                if (block == null) continue;
                
                float trackH = baseTrackHeight + (block.isExpanded ? expandedPropertyHeight : 0);
                
                // Draw Left Panel Item
                Rect leftItemRect = new Rect(0, currentY, leftPanelWidth, trackH);
                GUI.Box(leftItemRect, "", EditorStyles.helpBox);
                
                if (i == selectedBlockIndex)
                {
                    EditorGUI.DrawRect(new Rect(0, currentY, 4, trackH), Color.green);
                }
                
                Rect labelRect = new Rect(10, currentY + 10, leftPanelWidth - 15, 20);
                block.isExpanded = EditorGUI.Foldout(labelRect, block.isExpanded, $"{block.blockName} {(block.target != null ? $"({block.target.name})" : "")}", true);

                if (block.isExpanded)
                {
                    GUILayout.BeginArea(new Rect(20, currentY + baseTrackHeight, leftPanelWidth - 25, expandedPropertyHeight));
                    EditorGUI.BeginChangeCheck();
                    
                    // --- Type Switcher ---
                    var blockTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<DoTweenBlock>();
                    var validTypes = new System.Collections.Generic.List<System.Type>();
                    var typeNames = new System.Collections.Generic.List<string>();
                    int currentIndex = -1;
                    foreach(var t in blockTypes) 
                    {
                        if (!t.IsAbstract) 
                        {
                            if (t == block.GetType()) currentIndex = validTypes.Count;
                            validTypes.Add(t);
                            typeNames.Add(t.Name.Replace("DoTween", "").Replace("Block", ""));
                        }
                    }
                    
                    int newIndex = EditorGUILayout.Popup("Type", currentIndex, typeNames.ToArray());
                    if (newIndex != currentIndex && newIndex >= 0 && newIndex < validTypes.Count)
                    {
                        var newBlock = (DoTweenBlock)System.Activator.CreateInstance(validTypes[newIndex]);
                        newBlock.blockName = block.blockName; 
                        newBlock.startTime = block.startTime;
                        newBlock.duration = block.duration;
                        newBlock.easeType = block.easeType;
                        newBlock.target = block.target;
                        newBlock.isExpanded = block.isExpanded;
                        
                        Undo.RecordObject(targetTimeline, "Change Block Type");
                        targetTimeline.blocks[i] = newBlock;
                        EditorUtility.SetDirty(targetTimeline);
                        
                        GUILayout.EndArea();
                        GUI.EndScrollView();
                        GUIUtility.ExitGUI();
                    }
                    // --------------------

                    block.DrawInlineProperties();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(targetTimeline, "Block Property Change");
                        EditorUtility.SetDirty(targetTimeline);
                        GeneratePreviewSequence();
                    }
                    GUILayout.EndArea();
                }

                // Draw Timeline Track Background
                Rect trackBgRect = new Rect(leftPanelWidth, currentY, timelineContentWidth, trackH);
                GUI.Box(trackBgRect, "", EditorStyles.label);

                // Draw Block
                float blockX = leftPanelWidth + block.startTime * zoomScale;
                float blockW = block.duration * zoomScale;
                Rect blockRect = new Rect(blockX, currentY + 5, blockW, baseTrackHeight - 10);
                
                if (i == selectedBlockIndex)
                {
                    EditorGUI.DrawRect(new Rect(blockRect.x - 2, blockRect.y - 2, blockRect.width + 4, blockRect.height + 4), Color.green);
                }
                GUI.Box(blockRect, block.blockName, EditorStyles.toolbarButton); 
                
                // Handle events inside scroll view
                if (Event.current.type == EventType.MouseDown)
                {
                    if (leftItemRect.Contains(Event.current.mousePosition))
                    {
                        selectedBlockIndex = i;
                        Repaint();
                    }

                    Rect leftHandle = new Rect(blockRect.x, blockRect.y, 10, blockRect.height);
                    Rect rightHandle = new Rect(blockRect.xMax - 10, blockRect.y, 10, blockRect.height);
                    
                    if (leftHandle.Contains(Event.current.mousePosition))
                    {
                        selectedBlockIndex = i;
                        draggingBlockIndex = i;
                        currentDragType = DragType.LeftEdge;
                        initialDragMouseX = Event.current.mousePosition.x;
                        initialDragStartTime = block.startTime;
                        initialDragDuration = block.duration;
                        Undo.RecordObject(targetTimeline, "Resize Block Left");
                        Event.current.Use();
                    }
                    else if (rightHandle.Contains(Event.current.mousePosition))
                    {
                        selectedBlockIndex = i;
                        draggingBlockIndex = i;
                        currentDragType = DragType.RightEdge;
                        initialDragMouseX = Event.current.mousePosition.x;
                        initialDragDuration = block.duration;
                        Undo.RecordObject(targetTimeline, "Resize Block Right");
                        Event.current.Use();
                    }
                    else if (blockRect.Contains(Event.current.mousePosition))
                    {
                        selectedBlockIndex = i;
                        draggingBlockIndex = i;
                        currentDragType = DragType.Center;
                        initialDragMouseX = Event.current.mousePosition.x;
                        initialDragStartTime = block.startTime;
                        Undo.RecordObject(targetTimeline, "Move Block");
                        Event.current.Use();
                    }
                }

                currentY += trackH;
            }

            // Draw Playhead
            float playheadX = leftPanelWidth + playheadTime * zoomScale;
            Rect playheadRect = new Rect(playheadX - 1, 0, 2, Mathf.Max(totalHeight, position.height));
            EditorGUI.DrawRect(playheadRect, Color.red);
            
            // Draw Playhead Arrow
            if (Event.current.type == EventType.Repaint)
            {
                Vector3[] arrowPoints = new Vector3[]
                {
                    new Vector3(playheadX - 6, 0, 0),
                    new Vector3(playheadX + 6, 0, 0),
                    new Vector3(playheadX, 8, 0)
                };
                Handles.color = Color.red;
                Handles.DrawAAConvexPolygon(arrowPoints);
                Handles.color = Color.white;
            }
            
            GUI.EndScrollView();

            // Playhead click/drag detection (click on the timeline part)
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Rect timelineArea = new Rect(leftPanelWidth, headerHeight, position.width - leftPanelWidth, position.height - headerHeight);
                if (timelineArea.Contains(Event.current.mousePosition) && currentDragType == DragType.None)
                {
                    isDraggingPlayhead = true;
                    isPlaying = false; // Stop playback when manually scrubbing
                    SetPlayheadFromMouse(Event.current.mousePosition);
                    Event.current.Use();
                }
            }
        }

        private void SetPlayheadFromMouse(Vector2 mousePos)
        {
            float relativeX = mousePos.x - leftPanelWidth + scrollPosition.x;
            playheadTime = Mathf.Clamp(relativeX / zoomScale, 0f, targetTimeline.timelineDuration);
            UpdatePreview();
        }

        private void HandleEvents()
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDrag)
            {
                if (isDraggingPlayhead)
                {
                    SetPlayheadFromMouse(e.mousePosition);
                    Repaint();
                }
                else if (draggingBlockIndex >= 0 && draggingBlockIndex < targetTimeline.blocks.Count)
                {
                    var block = targetTimeline.blocks[draggingBlockIndex];
                    float deltaX = e.mousePosition.x - initialDragMouseX;
                    float deltaTime = deltaX / zoomScale;

                    if (currentDragType == DragType.Center)
                    {
                        block.startTime = Mathf.Max(0f, initialDragStartTime + deltaTime);
                    }
                    else if (currentDragType == DragType.LeftEdge)
                    {
                        float newStart = Mathf.Max(0f, initialDragStartTime + deltaTime);
                        float endT = initialDragStartTime + initialDragDuration;
                        if (newStart < endT)
                        {
                            block.startTime = newStart;
                            block.duration = endT - newStart;
                        }
                    }
                    else if (currentDragType == DragType.RightEdge)
                    {
                        block.duration = Mathf.Max(0.01f, initialDragDuration + deltaTime);
                    }

                    EditorUtility.SetDirty(targetTimeline);
                    Repaint();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                if (isDraggingPlayhead)
                {
                    isDraggingPlayhead = false;
                    Repaint();
                }
                else if (draggingBlockIndex >= 0)
                {
                    draggingBlockIndex = -1;
                    currentDragType = DragType.None;
                    // Rebuild sequence after moving blocks
                    GeneratePreviewSequence();
                    Repaint();
                }
            }
        }
    }
}
