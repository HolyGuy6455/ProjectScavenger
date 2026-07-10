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
    private TextField memoField;
    private Label childrenCountLabel;
    private PopupField<string> logicDropdown;
    private List<System.Type> logicTypesList = new List<System.Type>();
    
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
        leftPane.Add(scrollView);

        treeContainer = new VisualElement();
        treeContainer.style.alignItems = Align.Center;
        treeContainer.style.flexGrow = 1;
        scrollView.Add(treeContainer);

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

        inspectorTitle = new Label("Node Inspector");
        inspectorTitle.style.fontSize = 16;
        inspectorTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        inspectorTitle.style.marginBottom = 15;
        nodeTabContent.Add(inspectorTitle);

        memoField = new TextField("Memo (Name)");
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
            }
        });
        nodeTabContent.Add(logicDropdown);
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

            // 1. Type Dropdown
            List<string> types = new List<string> { "Numeric", "Boolean", "GameObject" };
            int currentTypeIndex = 0;
            if (prop is BTSharedBoolean) currentTypeIndex = 1;
            else if (prop is BTSharedGameObject) currentTypeIndex = 2;
            
            PopupField<string> typeDropdown = new PopupField<string>(types, currentTypeIndex);
            typeDropdown.style.width = 80;
            typeDropdown.RegisterValueChangedCallback(evt => 
            {
                BTSharedVariable newProp = null;
                if (evt.newValue == "Numeric") newProp = new BTSharedNumeric();
                else if (evt.newValue == "Boolean") newProp = new BTSharedBoolean();
                else if (evt.newValue == "GameObject") newProp = new BTSharedGameObject();
                
                newProp.key = ""; // Reset as requested
                currentBrain.blackboard[index] = newProp;
                EditorUtility.SetDirty(currentBrain);
                RebuildPropertyUI(); // Refresh UI
            });
            row.Add(typeDropdown);

            // 2. Key TextField
            TextField keyField = new TextField();
            keyField.value = prop.key;
            keyField.style.width = 60;
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

            if (valueField != null)
            {
                propertyValueFields.Add(valueField);
                row.Add(valueField);
            }

            // 4. Delete Button
            Button delBtn = new Button(() => {
                currentBrain.blackboard.RemoveAt(index);
                EditorUtility.SetDirty(currentBrain);
                RebuildPropertyUI();
            });
            delBtn.text = "X";
            delBtn.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            row.Add(delBtn);

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
            
            pLocal.y += edge.parent.worldBound.height / 2f;
            cLocal.y -= edge.child.worldBound.height / 2f;
            
            float midY = (pLocal.y + cLocal.y) / 2f;
            Vector3 p1 = pLocal;
            Vector3 p2 = new Vector3(pLocal.x, midY, 0);
            Vector3 p3 = new Vector3(cLocal.x, midY, 0);
            Vector3 p4 = cLocal;
            
            Handles.DrawPolyLine(p1, p2, p3, p4);
        }
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnSelectionChanged()
    {
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
        if (selectedNode == null)
        {
            inspectorTitle.text = "No Node Selected";
            memoField.style.display = DisplayStyle.None;
            logicDropdown.style.display = DisplayStyle.None;
            childrenCountLabel.text = "";
            return;
        }

        memoField.style.display = DisplayStyle.Flex;
        memoField.SetValueWithoutNotify(selectedNode.nodeName);

        if (selectedNode is BTAction act)
        {
            inspectorTitle.text = "Type: ACTION";
            childrenCountLabel.text = "Leaf Node (No Children)";
            
            logicDropdown.style.display = DisplayStyle.Flex;
            if (act.logic == null) logicDropdown.SetValueWithoutNotify("None");
            else logicDropdown.SetValueWithoutNotify(act.logic.GetType().Name);
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
        nodeHierarchy.style.flexDirection = FlexDirection.Column;
        nodeHierarchy.style.alignItems = Align.Center;
        nodeHierarchy.style.marginLeft = 10;
        nodeHierarchy.style.marginRight = 10;

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
        nodeBox.style.marginBottom = 40; 
        
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
        childrenContainer.style.flexDirection = FlexDirection.Row;
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
                plusHierarchy.style.flexDirection = FlexDirection.Column;
                plusHierarchy.style.alignItems = Align.Center;
                plusHierarchy.style.marginLeft = 5;
                plusHierarchy.style.marginRight = 5;

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
}
