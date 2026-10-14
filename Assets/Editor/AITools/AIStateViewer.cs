using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;

public class AIStateViewer : EditorWindow
{
    private AIBrain currentBrain;
    private VisualElement treeContainer;
    private ScrollView scrollView;
    private IMGUIContainer lineDrawer;
    
    // Inspector panel elements
    private VisualElement inspectorPanel;
    private VisualElement nodeTabContent;
    private VisualElement propertyTabContent;
    
    // Node Tab Elements
    private Label inspectorTitle;
    private TextField memoField; // Used for Name now
    private TextField nodeMemoField; // NEW: For longer memos
    private Label childrenCountLabel;
    private PopupField<string> logicDropdown;
    private List<System.Type> logicTypesList = new List<System.Type>();
    private VisualElement dynamicFieldsContainer;

    // Panning state
    private bool isDraggingTree = false;
    private Vector2 dragStartMousePos;
    private Vector2 dragStartScrollOffset; 

    // Zoom state
    private float currentZoom = 1.0f;
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 3.0f;
    private bool hasCenteredInitial = false;

    private void CenterViewToRoot()
    {
        if (scrollView != null)
        {
            scrollView.scrollOffset = new Vector2(5000 - scrollView.layout.width / 2, 5000 - scrollView.layout.height / 2);
        }
    }
    
    // Property Tab Elements
    private ScrollView propertyListScroll;
    private List<VisualElement> propertyValueFields = new List<VisualElement>(); // For real-time updates

    private BTNode selectedNode;
    private VisualElement selectedNodeBox;

    private enum TabState { Node, Property }
    private TabState currentTab = TabState.Node;

    // Edge data for drawing lines
    private class Edge
    {
        public VisualElement parent;
        public VisualElement child;
    }
    private List<Edge> edges = new List<Edge>();

    [MenuItem("Window/AI/State Viewer")]
    public static void ShowExample()
    {
        AIStateViewer wnd = GetWindow<AIStateViewer>();
        wnd.titleContent = new GUIContent("AI State Viewer");
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
        root.Add(splitView);

        // LEFT PANE: Tree View
        VisualElement leftPane = new VisualElement();
        
        Label titleLabel = new Label("Behavior Tree Viewer");
        titleLabel.style.fontSize = 20;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.alignSelf = Align.Center;
        titleLabel.style.marginTop = 10;
        titleLabel.style.marginBottom = 10;
        leftPane.Add(titleLabel);

        scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        scrollView.style.flexGrow = 1;
        scrollView.contentContainer.style.alignItems = Align.Center;
        scrollView.contentContainer.style.justifyContent = Justify.Center;
        leftPane.Add(scrollView);

        treeContainer = new VisualElement();
        treeContainer.style.alignItems = Align.Center;
        treeContainer.style.justifyContent = Justify.Center;
        treeContainer.style.width = 10000;
        treeContainer.style.height = 10000;
        scrollView.Add(treeContainer);

        // Center the scroll view
        scrollView.RegisterCallback<GeometryChangedEvent>(evt => {
            if (!hasCenteredInitial && scrollView.layout.width > 0)
            {
                CenterViewToRoot();
                hasCenteredInitial = true;
            }
        });

        // Home Key to center view
        root.RegisterCallback<KeyDownEvent>(evt => {
            if (evt.keyCode == KeyCode.Home)
            {
                CenterViewToRoot();
                evt.StopPropagation();
            }
        });

        // Setup Panning (Drag with Middle Mouse or Alt+Left Mouse)
        scrollView.RegisterCallback<PointerDownEvent>(evt => {
            if (evt.button == 2 || (evt.button == 0 && evt.altKey)) 
            {
                isDraggingTree = true;
                dragStartMousePos = evt.position;
                dragStartScrollOffset = scrollView.scrollOffset;
                scrollView.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        });

        scrollView.RegisterCallback<PointerMoveEvent>(evt => {
            if (isDraggingTree && scrollView.HasPointerCapture(evt.pointerId))
            {
                Vector2 delta = (Vector2)evt.position - dragStartMousePos;
                scrollView.scrollOffset = dragStartScrollOffset - delta;
                evt.StopPropagation();
            }
        });

        scrollView.RegisterCallback<PointerUpEvent>(evt => {
            if (isDraggingTree && scrollView.HasPointerCapture(evt.pointerId))
            {
                isDraggingTree = false;
                scrollView.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        });
        
        // Setup Shift+Scroll for horizontal and Ctrl+Scroll for Zoom
        scrollView.RegisterCallback<WheelEvent>(evt => {
            if (evt.ctrlKey || evt.commandKey)
            {
                currentZoom -= evt.delta.y * 0.05f;
                currentZoom = Mathf.Clamp(currentZoom, MinZoom, MaxZoom);
                treeContainer.style.scale = new StyleScale(new Scale(new Vector3(currentZoom, currentZoom, 1)));
                treeContainer.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                
                if (lineDrawer != null) lineDrawer.MarkDirtyRepaint();
                evt.StopPropagation();
            }
            else if (evt.shiftKey)
            {
                scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x + evt.delta.y * 20f, scrollView.scrollOffset.y);
                evt.StopPropagation();
            }
        });

        lineDrawer = new IMGUIContainer(DrawLines);
        lineDrawer.style.position = Position.Absolute;
        lineDrawer.style.top = 0;
        lineDrawer.style.bottom = 0;
        lineDrawer.style.left = 0;
        lineDrawer.style.right = 0;
        lineDrawer.pickingMode = PickingMode.Ignore;
        scrollView.contentContainer.Add(lineDrawer);
        lineDrawer.SendToBack();

        splitView.Add(leftPane);

        // RIGHT PANE: Inspector
        inspectorPanel = new VisualElement();
        inspectorPanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

        // Tabs Header
        VisualElement tabsHeader = new VisualElement();
        tabsHeader.style.flexDirection = FlexDirection.Row;
        tabsHeader.style.height = 30;
        
        Button nodeTabBtn = new Button(() => SwitchTab(TabState.Node)) { text = "Node" };
        nodeTabBtn.style.flexGrow = 1;
        Button propTabBtn = new Button(() => SwitchTab(TabState.Property)) { text = "Property" };
        propTabBtn.style.flexGrow = 1;
        
        tabsHeader.Add(nodeTabBtn);
        tabsHeader.Add(propTabBtn);
        inspectorPanel.Add(tabsHeader);

        // --- NODE TAB CONTENT ---
        nodeTabContent = new VisualElement();
        nodeTabContent.style.paddingTop = 10;
        nodeTabContent.style.paddingLeft = 10;
        nodeTabContent.style.paddingRight = 10;

        VisualElement headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.marginBottom = 15;

        inspectorTitle = new Label("Node Inspector");
        inspectorTitle.style.fontSize = 16;
        inspectorTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerRow.Add(inspectorTitle);

        Button deleteBtn = new Button(() => {
            if (selectedNode != null && currentBrain != null) {
                if (selectedNode == currentBrain.rootNode) {
                    currentBrain.rootNode = null;
                } else {
                    BTNode p = FindParent(currentBrain.rootNode, selectedNode);
                    if (p is BTSequence seq) seq.children.Remove(selectedNode);
                    else if (p is BTSelector sel) sel.children.Remove(selectedNode);
                }
                selectedNode = null;
                EditorUtility.SetDirty(currentBrain);
                RebuildTreeUI();
                UpdateInspector();
            }
        });
        deleteBtn.text = "Delete This Node";
        deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        deleteBtn.name = "deleteNodeBtn";

        Button refreshBtn = new Button(() => { UpdateInspector(); });
        refreshBtn.text = "Refresh";
        refreshBtn.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f);
        refreshBtn.style.marginTop = 5;
        refreshBtn.name = "refreshNodeBtn";

        VisualElement moveBtnContainer = new VisualElement();
        moveBtnContainer.style.flexDirection = FlexDirection.Row;
        moveBtnContainer.style.justifyContent = Justify.SpaceBetween;
        moveBtnContainer.style.marginBottom = 5;

        Button moveUpBtn = new Button(() => { MoveNode(selectedNode, -1); });
        moveUpBtn.text = "Move Up (↑)";
        moveUpBtn.name = "moveUpNodeBtn";
        moveUpBtn.style.flexGrow = 1;
        
        Button moveDownBtn = new Button(() => { MoveNode(selectedNode, 1); });
        moveDownBtn.text = "Move Down (↓)";
        moveDownBtn.name = "moveDownNodeBtn";
        moveDownBtn.style.flexGrow = 1;

        moveBtnContainer.Add(moveUpBtn);
        moveBtnContainer.Add(moveDownBtn);

        VisualElement btnContainer = new VisualElement();
        btnContainer.style.flexDirection = FlexDirection.Column;
        btnContainer.Add(moveBtnContainer);
        btnContainer.Add(deleteBtn);
        btnContainer.Add(refreshBtn);

        headerRow.Add(btnContainer);

        nodeTabContent.Add(headerRow);

        memoField = new TextField("Node Name");
        memoField.RegisterValueChangedCallback(evt => 
        {
            if (selectedNode != null)
            {
                selectedNode.nodeName = evt.newValue;
                if (currentBrain != null) EditorUtility.SetDirty(currentBrain);
                if (selectedNodeBox != null)
                {
                    Label nameLbl = selectedNodeBox.Q<Label>("nodeNameLabel");
                    if (nameLbl != null) nameLbl.text = evt.newValue;
                }
            }
        });
        nodeTabContent.Add(memoField);

        nodeMemoField = new TextField("Memo");
        nodeMemoField.multiline = true;
        nodeMemoField.style.minHeight = 60;
        nodeMemoField.style.whiteSpace = WhiteSpace.Normal;
        nodeMemoField.RegisterValueChangedCallback(evt => {
            if (selectedNode != null) {
                selectedNode.nodeMemo = evt.newValue;
                if (currentBrain != null) EditorUtility.SetDirty(currentBrain);
            }
        });
        nodeTabContent.Add(nodeMemoField);

        childrenCountLabel = new Label("");
        childrenCountLabel.style.marginTop = 10;
        nodeTabContent.Add(childrenCountLabel);

        // Populate Logic Types
        logicTypesList.Clear();
        List<string> typeNames = new List<string> { "None" };
        foreach(System.Type t in TypeCache.GetTypesDerivedFrom<BTActionLogic>())
        {
            if(!t.IsAbstract) 
            {
                logicTypesList.Add(t);
                typeNames.Add(t.Name);
            }
        }

        logicDropdown = new PopupField<string>("Action Logic", typeNames, 0);
        logicDropdown.style.marginTop = 10;
        logicDropdown.RegisterValueChangedCallback(evt => {
            if (selectedNode is BTAction act)
            {
                if (evt.newValue == "None") act.logic = null;
                else 
                {
                    System.Type t = logicTypesList.Find(x => x.Name == evt.newValue);
                    if (t != null) act.logic = (BTActionLogic)System.Activator.CreateInstance(t);
                }
                
                if (act.logic != null) 
                {
                    act.nodeName = evt.newValue;
                    memoField.SetValueWithoutNotify(evt.newValue);
                    if (selectedNodeBox != null)
                    {
                        Label nameLbl = selectedNodeBox.Q<Label>("nodeNameLabel");
                        if (nameLbl != null) nameLbl.text = evt.newValue;
                    }
                }
                if (currentBrain != null) EditorUtility.SetDirty(currentBrain);
                
                // Immediately update inspector to show new dynamic fields
                UpdateInspector();
            }
        });
        nodeTabContent.Add(logicDropdown);
        
        dynamicFieldsContainer = new VisualElement();
        dynamicFieldsContainer.style.marginTop = 15;
        nodeTabContent.Add(dynamicFieldsContainer);
        
        inspectorPanel.Add(nodeTabContent);

        // --- PROPERTY TAB CONTENT ---
        propertyTabContent = new VisualElement();
        propertyTabContent.style.paddingTop = 10;
        propertyTabContent.style.paddingLeft = 5;
        propertyTabContent.style.paddingRight = 5;
        propertyTabContent.style.display = DisplayStyle.None; // Hidden by default

        Label propTitle = new Label("Blackboard Properties");
        propTitle.style.fontSize = 16;
        propTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        propTitle.style.marginBottom = 10;
        propertyTabContent.Add(propTitle);

        propertyListScroll = new ScrollView(ScrollViewMode.Vertical);
        propertyListScroll.style.flexGrow = 1;
        propertyTabContent.Add(propertyListScroll);

        inspectorPanel.Add(propertyTabContent);
        splitView.Add(inspectorPanel);

        SwitchTab(TabState.Node);
        UpdateInspector();
        OnSelectionChanged();
    }

    private void SwitchTab(TabState newTab)
    {
        currentTab = newTab;
        if (currentTab == TabState.Node)
        {
            nodeTabContent.style.display = DisplayStyle.Flex;
            propertyTabContent.style.display = DisplayStyle.None;
        }
        else
        {
            nodeTabContent.style.display = DisplayStyle.None;
            propertyTabContent.style.display = DisplayStyle.Flex;
            RebuildPropertyUI();
        }
    }

    private void RebuildPropertyUI()
    {
        propertyListScroll.Clear();
        propertyValueFields.Clear();

        if (currentBrain == null)
        {
            propertyListScroll.Add(new Label("No AIBrain selected."));
            return;
        }

        if (currentBrain.blackboard == null)
            currentBrain.blackboard = new List<BTSharedVariable>();

        for (int i = 0; i < currentBrain.blackboard.Count; i++)
        {
            int index = i; // capture for lambda
            BTSharedVariable prop = currentBrain.blackboard[i];
            
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 5;
            row.style.alignItems = Align.Center;

            List<string> types = new List<string> { "Numeric", "Boolean", "GameObject", "String", "Vector3" };
            int currentTypeIndex = 0;
            if (prop is BTSharedBoolean) currentTypeIndex = 1;
            else if (prop is BTSharedGameObject) currentTypeIndex = 2;
            else if (prop is BTSharedString) currentTypeIndex = 3;
            else if (prop is BTSharedVector3) currentTypeIndex = 4;
            
            PopupField<string> typeDropdown = new PopupField<string>(types, currentTypeIndex);
            typeDropdown.style.width = 80;
            typeDropdown.RegisterValueChangedCallback(evt => 
            {
                BTSharedVariable newProp = null;
                if (evt.newValue == "Numeric") newProp = new BTSharedNumeric();
                else if (evt.newValue == "Boolean") newProp = new BTSharedBoolean();
                else if (evt.newValue == "GameObject") newProp = new BTSharedGameObject();
                else if (evt.newValue == "String") newProp = new BTSharedString();
                else if (evt.newValue == "Vector3") newProp = new BTSharedVector3();
                
                newProp.key = ""; // Reset as requested
                currentBrain.blackboard[index] = newProp;
                EditorUtility.SetDirty(currentBrain);
                RebuildPropertyUI(); // Refresh UI
            });
            row.Add(typeDropdown);

            // 2. Key TextField
            TextField keyField = new TextField();
            keyField.value = prop.key;
            keyField.style.width = 100;
            keyField.RegisterValueChangedCallback(evt => {
                currentBrain.blackboard[index].key = evt.newValue;
                EditorUtility.SetDirty(currentBrain);
            });
            row.Add(keyField);

            // 3. Value Field
            VisualElement valueField = null;
            if (prop is BTSharedNumeric numProp)
            {
                DoubleField df = new DoubleField();
                df.value = numProp.value;
                df.style.flexGrow = 1;
                df.RegisterValueChangedCallback(evt => {
                    numProp.value = evt.newValue;
                    EditorUtility.SetDirty(currentBrain);
                });
                df.userData = numProp; // Link for live update
                valueField = df;
            }
            else if (prop is BTSharedBoolean boolProp)
            {
                Toggle tg = new Toggle();
                tg.value = boolProp.value;
                tg.style.flexGrow = 1;
                tg.RegisterValueChangedCallback(evt => {
                    boolProp.value = evt.newValue;
                    EditorUtility.SetDirty(currentBrain);
                });
                tg.userData = boolProp;
                valueField = tg;
            }
            else if (prop is BTSharedGameObject goProp)
            {
                ObjectField of = new ObjectField();
                of.objectType = typeof(GameObject);
                of.value = goProp.value;
                of.style.flexGrow = 1;
                of.RegisterValueChangedCallback(evt => {
                    goProp.value = (GameObject)evt.newValue;
                    EditorUtility.SetDirty(currentBrain);
                });
                of.userData = goProp;
                valueField = of;
            }
            else if (prop is BTSharedString strProp)
            {
                TextField tf = new TextField();
                tf.value = strProp.value;
                tf.style.flexGrow = 1;
                tf.RegisterValueChangedCallback(evt => {
                    strProp.value = evt.newValue;
                    EditorUtility.SetDirty(currentBrain);
                });
                tf.userData = strProp;
                valueField = tf;
            }
            else if (prop is BTSharedVector3 vecProp)
            {
                UnityEngine.UIElements.Vector3Field vf = new UnityEngine.UIElements.Vector3Field();
                vf.value = vecProp.value;
                vf.style.flexGrow = 1;
                vf.RegisterValueChangedCallback(evt => {
                    vecProp.value = evt.newValue;
                    EditorUtility.SetDirty(currentBrain);
                });
                vf.userData = vecProp;
                valueField = vf;
            }

            if (valueField != null)
            {
                propertyValueFields.Add(valueField);
                row.Add(valueField);
            }

            // 4. Up/Down/Delete Buttons Container
            VisualElement actionBtnContainer = new VisualElement();
            actionBtnContainer.style.flexDirection = FlexDirection.Row;

            Button upBtn = new Button(() => {
                if (index > 0) {
                    var temp = currentBrain.blackboard[index];
                    currentBrain.blackboard[index] = currentBrain.blackboard[index - 1];
                    currentBrain.blackboard[index - 1] = temp;
                    EditorUtility.SetDirty(currentBrain);
                    RebuildPropertyUI();
                }
            });
            upBtn.text = "↑";
            upBtn.style.width = 25;
            
            Button downBtn = new Button(() => {
                if (index < currentBrain.blackboard.Count - 1) {
                    var temp = currentBrain.blackboard[index];
                    currentBrain.blackboard[index] = currentBrain.blackboard[index + 1];
                    currentBrain.blackboard[index + 1] = temp;
                    EditorUtility.SetDirty(currentBrain);
                    RebuildPropertyUI();
                }
            });
            downBtn.text = "↓";
            downBtn.style.width = 25;

            Button delBtn = new Button(() => {
                currentBrain.blackboard.RemoveAt(index);
                EditorUtility.SetDirty(currentBrain);
                RebuildPropertyUI();
            });
            delBtn.text = "X";
            delBtn.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            delBtn.style.width = 25;

            actionBtnContainer.Add(upBtn);
            actionBtnContainer.Add(downBtn);
            actionBtnContainer.Add(delBtn);

            row.Add(actionBtnContainer);

            propertyListScroll.Add(row);
        }

        // Add Button
        Button addBtn = new Button(() => {
            currentBrain.blackboard.Add(new BTSharedNumeric() { key = "" });
            EditorUtility.SetDirty(currentBrain);
            RebuildPropertyUI();
        });
        addBtn.text = "+ Add Property";
        addBtn.style.marginTop = 10;
        addBtn.style.height = 25;
        propertyListScroll.Add(addBtn);
    }

    private void DrawLines()
    {
        if (edges == null || edges.Count == 0) return;
        Handles.color = new Color(0.6f, 0.6f, 0.6f, 1f); 
        
        foreach(var edge in edges)
        {
            if (edge.parent == null || edge.child == null) continue;
            Vector2 parentCenter = edge.parent.worldBound.center;
            Vector2 childCenter = edge.child.worldBound.center;
            
            Vector2 pLocal = lineDrawer.WorldToLocal(parentCenter);
            Vector2 cLocal = lineDrawer.WorldToLocal(childCenter);
            
            pLocal.x += edge.parent.worldBound.width / 2f;
            cLocal.x -= edge.child.worldBound.width / 2f;
            
            float midX = (pLocal.x + cLocal.x) / 2f;
            Vector3 p1 = pLocal;
            Vector3 p2 = new Vector3(midX, pLocal.y, 0);
            Vector3 p3 = new Vector3(midX, cLocal.y, 0);
            Vector3 p4 = cLocal;
            
            Handles.DrawPolyLine(p1, p2, p3, p4);
        }
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        Undo.undoRedoPerformed += OnSelectionChanged;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnSelectionChanged;
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnSelectionChanged()
    {
        if (inspectorTitle == null) return;
        
        GameObject selectedObj = Selection.activeGameObject;
        currentBrain = selectedObj != null ? selectedObj.GetComponent<AIBrain>() : null;
        
        selectedNode = null;
        selectedNodeBox = null;
        UpdateInspector();
        RebuildTreeUI();
        
        if (currentTab == TabState.Property) RebuildPropertyUI();
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying && currentBrain != null)
        {
            RebuildTreeUI(); 
            
            // Live update properties
            if (currentTab == TabState.Property)
            {
                foreach(var field in propertyValueFields)
                {
                    if (field.focusController != null && field.focusController.focusedElement == field) 
                        continue; // Skip updating if user is currently typing in it

                    if (field is DoubleField df && df.userData is BTSharedNumeric num)
                        df.SetValueWithoutNotify(num.value);
                    else if (field is Toggle tg && tg.userData is BTSharedBoolean bln)
                        tg.SetValueWithoutNotify(bln.value);
                    else if (field is ObjectField of && of.userData is BTSharedGameObject go)
                        of.SetValueWithoutNotify(go.value);
                    else if (field is TextField tf && tf.userData is BTSharedString str)
                        tf.SetValueWithoutNotify(str.value);
                    else if (field is UnityEngine.UIElements.Vector3Field vf && vf.userData is BTSharedVector3 vec)
                        vf.SetValueWithoutNotify(vec.value);
                }
            }
        }
    }

    private void SelectNode(BTNode node, VisualElement nodeBox)
    {
        if (selectedNodeBox != null)
        {
            selectedNodeBox.style.borderTopColor = Color.black;
            selectedNodeBox.style.borderBottomColor = Color.black;
            selectedNodeBox.style.borderLeftColor = Color.black;
            selectedNodeBox.style.borderRightColor = Color.black;
        }

        selectedNode = node;
        selectedNodeBox = nodeBox;

        if (selectedNodeBox != null)
        {
            selectedNodeBox.style.borderTopColor = Color.yellow;
            selectedNodeBox.style.borderBottomColor = Color.yellow;
            selectedNodeBox.style.borderLeftColor = Color.yellow;
            selectedNodeBox.style.borderRightColor = Color.yellow;
        }

        UpdateInspector();
        if (currentTab != TabState.Node) SwitchTab(TabState.Node); // Auto-switch to node tab
    }

    private void UpdateInspector()
    {
        if (dynamicFieldsContainer != null) dynamicFieldsContainer.Clear();

        if (selectedNode == null)
        {
            inspectorTitle.text = "No Node Selected";
            memoField.style.display = DisplayStyle.None;
            if (nodeMemoField != null) nodeMemoField.style.display = DisplayStyle.None;
            logicDropdown.style.display = DisplayStyle.None;
            childrenCountLabel.text = "";
            Button delBtn = nodeTabContent.Q<Button>("deleteNodeBtn");
            if (delBtn != null) delBtn.style.display = DisplayStyle.None;
            Button refBtn = nodeTabContent.Q<Button>("refreshNodeBtn");
            if (refBtn != null) refBtn.style.display = DisplayStyle.None;
            Button upNodeBtn = nodeTabContent.Q<Button>("moveUpNodeBtn");
            if (upNodeBtn != null) upNodeBtn.style.display = DisplayStyle.None;
            Button downNodeBtn = nodeTabContent.Q<Button>("moveDownNodeBtn");
            if (downNodeBtn != null) downNodeBtn.style.display = DisplayStyle.None;
            return;
        }

        memoField.style.display = DisplayStyle.Flex;
        memoField.SetValueWithoutNotify(selectedNode.nodeName);

        if (nodeMemoField != null)
        {
            nodeMemoField.style.display = DisplayStyle.Flex;
            nodeMemoField.SetValueWithoutNotify(selectedNode.nodeMemo);
        }

        Button delBtn2 = nodeTabContent.Q<Button>("deleteNodeBtn");
        if (delBtn2 != null) delBtn2.style.display = DisplayStyle.Flex;

        Button refBtn2 = nodeTabContent.Q<Button>("refreshNodeBtn");
        if (refBtn2 != null) refBtn2.style.display = DisplayStyle.Flex;

        bool hasParent = (currentBrain != null && currentBrain.rootNode != selectedNode && FindParent(currentBrain.rootNode, selectedNode) != null);
        Button upNodeBtn2 = nodeTabContent.Q<Button>("moveUpNodeBtn");
        if (upNodeBtn2 != null) upNodeBtn2.style.display = hasParent ? DisplayStyle.Flex : DisplayStyle.None;
        Button downNodeBtn2 = nodeTabContent.Q<Button>("moveDownNodeBtn");
        if (downNodeBtn2 != null) downNodeBtn2.style.display = hasParent ? DisplayStyle.Flex : DisplayStyle.None;

        if (selectedNode is BTAction act)
        {
            inspectorTitle.text = "Type: ACTION";
            childrenCountLabel.text = "Leaf Node (No Children)";
            
            logicDropdown.style.display = DisplayStyle.Flex;
            if (act.logic == null) logicDropdown.SetValueWithoutNotify("None");
            else logicDropdown.SetValueWithoutNotify(act.logic.GetType().Name);

            // Populate dynamic reflection fields for logic
            if (act.logic != null)
            {
                string desc = act.logic.GetDescription(currentBrain);
                HelpBox descBox = new HelpBox(desc, HelpBoxMessageType.Info) { name = "logicDescription" };
                descBox.style.marginBottom = 10;
                descBox.style.marginTop = 10;
                descBox.style.display = string.IsNullOrEmpty(desc) ? DisplayStyle.None : DisplayStyle.Flex;
                dynamicFieldsContainer.Add(descBox);

                Label logicTitle = new Label("Logic Settings");
                logicTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                logicTitle.style.marginBottom = 5;
                logicTitle.style.color = new Color(0.7f, 0.9f, 0.7f);
                dynamicFieldsContainer.Add(logicTitle);

                // Collect available keys from blackboard
                List<string> availableKeys = new List<string> { "" };
                if (currentBrain != null && currentBrain.blackboard != null)
                {
                    foreach (var prop in currentBrain.blackboard)
                    {
                        if (!string.IsNullOrEmpty(prop.key))
                            availableKeys.Add(prop.key);
                    }
                }

                System.Reflection.FieldInfo[] fields = act.logic.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (typeof(BTInput).IsAssignableFrom(field.FieldType))
                    {
                        DrawInputField(act, field, (BTInput)field.GetValue(act.logic));
                        continue;
                    }

                    if (field.FieldType == typeof(string))
                    {
                        string currentValue = (string)field.GetValue(act.logic);
                        VisualElement fieldRow = new VisualElement();
                        fieldRow.style.flexDirection = FlexDirection.Row;
                        fieldRow.style.marginBottom = 2;

                        Label fieldLabel = new Label(field.Name);
                        fieldLabel.style.width = 145; 
                        fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                        var fieldKeys = new List<string>(availableKeys);
                        if (currentValue != null && !fieldKeys.Contains(currentValue))
                            fieldKeys.Add(currentValue);
                        
                        // Button + GenericMenu로 "AAA/BBB" 형태의 Blackboard 키 경로를 표시한다.
                        Button fieldDropdownBtn = new Button();
                        fieldDropdownBtn.name = field.Name;
                        fieldDropdownBtn.tooltip = "Blackboard에서 읽거나 쓸 변수의 키입니다.";
                        fieldDropdownBtn.text = string.IsNullOrEmpty(currentValue) ? "(None)" : currentValue;
                        fieldDropdownBtn.style.flexGrow = 1;
                        fieldDropdownBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                        
                        fieldDropdownBtn.clicked += () => 
                        {
                            GenericMenu menu = new GenericMenu();
                            foreach(string key in fieldKeys)
                            {
                                string menuPath = string.IsNullOrEmpty(key) ? "None" : key;
                                menu.AddItem(new GUIContent(menuPath), key == currentValue, () => 
                                {
                                    field.SetValue(act.logic, key);
                                    fieldDropdownBtn.text = string.IsNullOrEmpty(key) ? "(None)" : key;
                                    EditorUtility.SetDirty(currentBrain);
                                    UpdateInspector(); // Refresh description and UI
                                });
                            }
                            menu.ShowAsContext();
                        };

                        fieldRow.Add(fieldLabel);
                        fieldRow.Add(fieldDropdownBtn);
                        dynamicFieldsContainer.Add(fieldRow);
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        VisualElement fieldRow = new VisualElement();
                        fieldRow.style.flexDirection = FlexDirection.Row;
                        fieldRow.style.marginBottom = 5;

                        Label fieldLabel = new Label(field.Name);
                        fieldLabel.style.width = 145;
                        fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                        Toggle toggleField = new Toggle();
                        toggleField.value = (bool)field.GetValue(act.logic);
                        toggleField.RegisterValueChangedCallback(evt => {
                            field.SetValue(act.logic, evt.newValue);
                            EditorUtility.SetDirty(currentBrain);
                            UpdateInspector();
                        });

                        fieldRow.Add(fieldLabel);
                        fieldRow.Add(toggleField);
                        dynamicFieldsContainer.Add(fieldRow);
                    }
                    else if (field.FieldType == typeof(float))
                    {
                        VisualElement fieldRow = new VisualElement();
                        fieldRow.style.flexDirection = FlexDirection.Row;
                        fieldRow.style.marginBottom = 5;

                        Label fieldLabel = new Label(field.Name);
                        fieldLabel.style.width = 145;
                        fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                        UnityEngine.UIElements.FloatField floatField = new UnityEngine.UIElements.FloatField();
                        floatField.value = (float)field.GetValue(act.logic);
                        floatField.style.flexGrow = 1;
                        floatField.RegisterValueChangedCallback(evt => {
                            field.SetValue(act.logic, evt.newValue);
                            EditorUtility.SetDirty(currentBrain);
                            UpdateInspector();
                        });

                        fieldRow.Add(fieldLabel);
                        fieldRow.Add(floatField);
                        dynamicFieldsContainer.Add(fieldRow);
                    }
                    else if (field.FieldType == typeof(LayerMask))
                    {
                        var editedBrain = currentBrain;
                        var editedLogic = act.logic;
                        var fieldRow = new VisualElement();
                        fieldRow.style.flexDirection = FlexDirection.Row;
                        fieldRow.style.marginBottom = 5;

                        var fieldLabel = new Label(field.Name);
                        fieldLabel.style.width = 145;
                        fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                        var maskField = new LayerMaskField { name = field.Name };
                        maskField.SetValueWithoutNotify(((LayerMask)field.GetValue(editedLogic)).value);
                        maskField.style.flexGrow = 1;
                        maskField.RegisterValueChangedCallback(evt =>
                        {
                            if (editedBrain == null || currentBrain != editedBrain
                                || selectedNode != act || act.logic != editedLogic) return;
                            Undo.RecordObject(editedBrain, "Edit " + field.Name);
                            field.SetValue(editedLogic, (LayerMask)evt.newValue);
                            EditorUtility.SetDirty(editedBrain);
                            if (PrefabUtility.IsPartOfPrefabInstance(editedBrain))
                                PrefabUtility.RecordPrefabInstancePropertyModifications(editedBrain);

                            var description = dynamicFieldsContainer.Q<HelpBox>("logicDescription");
                            if (description != null) description.text = editedLogic.GetDescription(editedBrain);
                        });

                        fieldRow.Add(fieldLabel);
                        fieldRow.Add(maskField);
                        dynamicFieldsContainer.Add(fieldRow);
                    }
                    else if (field.FieldType.IsEnum)
                    {
                        VisualElement fieldRow = new VisualElement();
                        fieldRow.style.flexDirection = FlexDirection.Row;
                        fieldRow.style.marginBottom = 5;

                        Label fieldLabel = new Label(field.Name);
                        fieldLabel.style.width = 145;
                        fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                        UnityEngine.UIElements.EnumField enumField = new UnityEngine.UIElements.EnumField((System.Enum)field.GetValue(act.logic));
                        enumField.style.flexGrow = 1;
                        enumField.RegisterValueChangedCallback(evt => {
                            field.SetValue(act.logic, evt.newValue);
                            EditorUtility.SetDirty(currentBrain);
                            UpdateInspector();
                        });

                        fieldRow.Add(fieldLabel);
                        fieldRow.Add(enumField);
                        dynamicFieldsContainer.Add(fieldRow);
                    }
                }
            }
        }
        else if (selectedNode is BTSequence seq)
        {
            inspectorTitle.text = "Type: SEQUENCE";
            childrenCountLabel.text = $"Children Count: {seq.children.Count}";
            logicDropdown.style.display = DisplayStyle.None;
        }
        else if (selectedNode is BTSelector sel)
        {
            inspectorTitle.text = "Type: SELECTOR";
            childrenCountLabel.text = $"Children Count: {sel.children.Count}";
            logicDropdown.style.display = DisplayStyle.None;
        }
    }

    private void DrawInputField(BTAction action, System.Reflection.FieldInfo field, BTInput input)
    {
        var editedBrain = currentBrain;
        var editedLogic = action.logic;
        bool wasNull = input == null;
        if (input == null)
        {
            if (field.FieldType.IsAbstract) return;
            input = (BTInput)System.Activator.CreateInstance(field.FieldType);
        }

        System.Func<bool> canEdit = () => editedBrain != null && currentBrain == editedBrain
            && selectedNode == action && action.logic == editedLogic
            && (ReferenceEquals(field.GetValue(editedLogic), input)
                || (wasNull && field.GetValue(editedLogic) == null));
        System.Action<System.Action, bool> commit = (change, rebuild) =>
        {
            if (!canEdit()) return;
            Undo.RecordObject(editedBrain, "Edit " + field.Name + " Input");
            field.SetValue(editedLogic, input);
            change();
            EditorUtility.SetDirty(editedBrain);
            if (PrefabUtility.IsPartOfPrefabInstance(editedBrain))
                PrefabUtility.RecordPrefabInstancePropertyModifications(editedBrain);
            if (rebuild)
            {
                UpdateInspector();
            }
            else
            {
                // 값 입력 중에는 컨트롤을 재생성하지 않아 키보드 포커스를 유지한다.
                var description = dynamicFieldsContainer.Q<HelpBox>("logicDescription");
                if (description != null)
                {
                    description.text = editedLogic.GetDescription(editedBrain);
                    description.style.display = string.IsNullOrEmpty(description.text)
                        ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
        };

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 2;
        var label = new Label(field.Name);
        label.style.width = 145;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(label);

        if (input.useDirectValue)
        {
            VisualElement valueField;
            if (input.ValueType == typeof(string))
            {
                var text = new TextField { isDelayed = true };
                text.SetValueWithoutNotify((string)input.DirectValue ?? "");
                text.RegisterValueChangedCallback(evt => commit(() => input.DirectValue = evt.newValue, false));
                valueField = text;
            }
            else if (input.ValueType == typeof(double))
            {
                var number = new DoubleField { isDelayed = true };
                number.SetValueWithoutNotify((double)input.DirectValue);
                number.RegisterValueChangedCallback(evt => commit(() => input.DirectValue = evt.newValue, false));
                valueField = number;
            }
            else if (input.ValueType == typeof(bool))
            {
                var boolean = new Toggle();
                boolean.SetValueWithoutNotify((bool)input.DirectValue);
                boolean.RegisterValueChangedCallback(evt => commit(() => input.DirectValue = evt.newValue, false));
                valueField = boolean;
            }
            else if (input.ValueType == typeof(Vector3))
            {
                var vector = new Vector3Field();
                vector.SetValueWithoutNotify((Vector3)input.DirectValue);
                vector.RegisterValueChangedCallback(evt => commit(() => input.DirectValue = evt.newValue, false));
                valueField = vector;
            }
            else if (input.ValueType == typeof(GameObject))
            {
                var gameObject = new ObjectField { objectType = typeof(GameObject), allowSceneObjects = true };
                gameObject.SetValueWithoutNotify((GameObject)input.DirectValue);
                gameObject.RegisterValueChangedCallback(evt => commit(() => input.DirectValue = (GameObject)evt.newValue, false));
                valueField = gameObject;
            }
            else
            {
                valueField = new Label("지원하지 않는 입력 타입: " + input.ValueType.Name);
            }
            valueField.name = field.Name + ".directValue";
            valueField.tooltip = "직접 입력한 값을 사용합니다.";
            valueField.style.flexGrow = 1;
            row.Add(valueField);
        }
        else
        {
            var dropdown = new Button { name = field.Name + ".blackboardKey" };
            dropdown.text = string.IsNullOrEmpty(input.blackboardKey) ? "(None)" : input.blackboardKey;
            dropdown.tooltip = "선택한 Blackboard 변수의 값을 사용합니다.";
            dropdown.style.flexGrow = 1;
            dropdown.style.unityTextAlign = TextAnchor.MiddleLeft;
            dropdown.clicked += () =>
            {
                if (!canEdit()) return;
                var keys = new List<string> { "" };
                if (editedBrain.blackboard != null)
                {
                    foreach (var variable in editedBrain.blackboard)
                        if (input.BlackboardVariableType.IsInstanceOfType(variable) && !string.IsNullOrEmpty(variable.key)
                            && !keys.Contains(variable.key)) keys.Add(variable.key);
                }
                var menu = new GenericMenu();
                foreach (string key in keys)
                {
                    string menuPath = string.IsNullOrEmpty(key) ? "None" : key;
                    menu.AddItem(new GUIContent(menuPath), key == input.blackboardKey,
                        () => commit(() => input.blackboardKey = key, true));
                }
                menu.ShowAsContext();
            };
            row.Add(dropdown);
        }

        var directToggle = new Toggle("직접 입력") { name = field.Name + ".useDirectValue" };
        directToggle.tooltip = "체크: 직접 입력한 값 사용 / 체크 해제: Blackboard 값 사용";
        directToggle.SetValueWithoutNotify(input.useDirectValue);
        directToggle.style.marginLeft = 5;
        directToggle.RegisterValueChangedCallback(evt => commit(() => input.useDirectValue = evt.newValue, true));
        row.Add(directToggle);
        dynamicFieldsContainer.Add(row);
    }

    private void RebuildTreeUI()
    {
        treeContainer.Clear();
        edges.Clear();

        if (currentBrain == null)
        {
            treeContainer.Add(new Label("No AIBrain selected."));
            return;
        }

        if (currentBrain.rootNode == null)
        {
            Button createRootBtn = new Button(() => 
            {
                currentBrain.rootNode = new BTSelector();
                EditorUtility.SetDirty(currentBrain);
                RebuildTreeUI();
            });
            createRootBtn.text = "Create Root Node (Selector)";
            treeContainer.Add(createRootBtn);
            return;
        }

        treeContainer.Add(DrawNode(currentBrain.rootNode, currentBrain, null, -1, null));
    }

    private VisualElement DrawNode(BTNode node, AIBrain brain, BTNode parent, int childIndex, VisualElement parentNodeBox)
    {
        VisualElement nodeHierarchy = new VisualElement();
        nodeHierarchy.style.flexDirection = FlexDirection.Row;
        nodeHierarchy.style.alignItems = Align.Center;
        nodeHierarchy.style.marginTop = 10;
        nodeHierarchy.style.marginBottom = 10;

        VisualElement nodeBox = new VisualElement();
        nodeBox.style.width = 120; 
        nodeBox.style.height = 70;
        nodeBox.style.borderTopWidth = 2;
        nodeBox.style.borderBottomWidth = 2;
        nodeBox.style.borderLeftWidth = 2;
        nodeBox.style.borderRightWidth = 2;
        nodeBox.style.borderTopColor = Color.black;
        nodeBox.style.borderBottomColor = Color.black;
        nodeBox.style.borderLeftColor = Color.black;
        nodeBox.style.borderRightColor = Color.black;
        nodeBox.style.alignItems = Align.Center;
        nodeBox.style.justifyContent = Justify.FlexStart; 
        nodeBox.style.marginRight = 40; 
        
        if (selectedNode == node)
        {
            nodeBox.style.borderTopColor = Color.yellow;
            nodeBox.style.borderBottomColor = Color.yellow;
            nodeBox.style.borderLeftColor = Color.yellow;
            nodeBox.style.borderRightColor = Color.yellow;
            selectedNodeBox = nodeBox;
        }

        if (Application.isPlaying)
        {
            if (node.State == NodeState.RUNNING) nodeBox.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            else if (node.State == NodeState.SUCCESS) nodeBox.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
            else if (node.State == NodeState.FAILURE) nodeBox.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            else nodeBox.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
        }
        else
        {
            nodeBox.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
        }

        Label typeLabel = new Label();
        typeLabel.style.fontSize = 10;
        typeLabel.style.marginTop = 2;
        typeLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
        if (node is BTAction) typeLabel.text = "ACT";
        else if (node is BTSequence) typeLabel.text = "SEQ";
        else if (node is BTSelector) typeLabel.text = "SEL";
        nodeBox.Add(typeLabel);

        Label nameLabel = new Label(node.nodeName);
        nameLabel.name = "nodeNameLabel"; 
        nameLabel.style.fontSize = 14;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.color = Color.white;
        nameLabel.style.whiteSpace = WhiteSpace.Normal; 
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.flexGrow = 1;
        nodeBox.Add(nameLabel);

        nodeBox.RegisterCallback<MouseDownEvent>(evt => 
        {
            if (evt.button == 0) 
            {
                SelectNode(node, nodeBox);
                evt.StopPropagation();
            }
        });

        if (parent != null && !Application.isPlaying)
        {
            nodeBox.RegisterCallback<ContextClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Move Up"), false, () => { MoveNode(node, -1); });
                menu.AddItem(new GUIContent("Move Down"), false, () => { MoveNode(node, 1); });
                menu.AddItem(new GUIContent("Delete Node"), false, () => 
                {
                    if (parent is BTSequence seq) seq.children.RemoveAt(childIndex);
                    else if (parent is BTSelector sel) sel.children.RemoveAt(childIndex);
                    
                    if (selectedNode == node) SelectNode(null, null); 
                    
                    EditorUtility.SetDirty(brain);
                    RebuildTreeUI();
                });
                menu.ShowAsContext();
            });
        }

        nodeHierarchy.Add(nodeBox);

        if (parentNodeBox != null)
        {
            edges.Add(new Edge { parent = parentNodeBox, child = nodeBox });
        }

        VisualElement childrenContainer = new VisualElement();
        childrenContainer.style.flexDirection = FlexDirection.Column;
        childrenContainer.style.justifyContent = Justify.Center;

        bool hasChildrenList = false;
        List<BTNode> childrenList = null;

        if (node is BTSequence seqNode)
        {
            hasChildrenList = true;
            childrenList = seqNode.children;
        }
        else if (node is BTSelector selNode)
        {
            hasChildrenList = true;
            childrenList = selNode.children;
        }

        if (hasChildrenList && childrenList != null)
        {
            for (int i = 0; i < childrenList.Count; i++)
            {
                if (childrenList[i] != null)
                {
                    childrenContainer.Add(DrawNode(childrenList[i], brain, node, i, nodeBox));
                }
            }

            if (!Application.isPlaying)
            {
                VisualElement plusHierarchy = new VisualElement();
                plusHierarchy.style.flexDirection = FlexDirection.Row;
                plusHierarchy.style.alignItems = Align.Center;
                plusHierarchy.style.marginTop = 5;
                plusHierarchy.style.marginBottom = 5;

                Button plusBtn = new Button();
                plusBtn.style.width = 30;
                plusBtn.style.height = 30;
                plusBtn.style.backgroundColor = new Color(0.2f, 0.4f, 0.2f);
                plusBtn.style.alignItems = Align.Center;
                plusBtn.style.justifyContent = Justify.Center;
                plusBtn.text = "+";
                plusBtn.style.fontSize = 16;
                plusBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                
                plusBtn.clicked += () => 
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Action (ACT)"), false, () => { childrenList.Add(new BTAction()); EditorUtility.SetDirty(brain); RebuildTreeUI(); });
                    menu.AddItem(new GUIContent("Add Sequence (SEQ)"), false, () => { childrenList.Add(new BTSequence()); EditorUtility.SetDirty(brain); RebuildTreeUI(); });
                    menu.AddItem(new GUIContent("Add Selector (SEL)"), false, () => { childrenList.Add(new BTSelector()); EditorUtility.SetDirty(brain); RebuildTreeUI(); });
                    menu.ShowAsContext();
                };

                edges.Add(new Edge { parent = nodeBox, child = plusBtn });
                
                plusHierarchy.Add(plusBtn);
                childrenContainer.Add(plusHierarchy);
            }
        }

        if (childrenContainer.childCount > 0)
        {
            nodeHierarchy.Add(childrenContainer);
        }

        return nodeHierarchy;
    }

    private BTNode FindParent(BTNode current, BTNode target)
    {
        if (current == null) return null;
        if (current is BTSequence seq)
        {
            if (seq.children.Contains(target)) return current;
            foreach (var c in seq.children) { var p = FindParent(c, target); if (p != null) return p; }
        }
        else if (current is BTSelector sel)
        {
            if (sel.children.Contains(target)) return current;
            foreach (var c in sel.children) { var p = FindParent(c, target); if (p != null) return p; }
        }
        return null;
    }

    private void MoveNode(BTNode node, int offset)
    {
        if (node == null || currentBrain == null || currentBrain.rootNode == node) return;
        BTNode parent = FindParent(currentBrain.rootNode, node);
        if (parent == null) return;

        List<BTNode> childrenList = null;
        if (parent is BTSequence seq) childrenList = seq.children;
        else if (parent is BTSelector sel) childrenList = sel.children;

        if (childrenList != null)
        {
            int index = childrenList.IndexOf(node);
            int newIndex = index + offset;
            if (newIndex >= 0 && newIndex < childrenList.Count)
            {
                var temp = childrenList[index];
                childrenList[index] = childrenList[newIndex];
                childrenList[newIndex] = temp;

                EditorUtility.SetDirty(currentBrain);
                RebuildTreeUI();
                UpdateInspector();
            }
        }
    }
}
