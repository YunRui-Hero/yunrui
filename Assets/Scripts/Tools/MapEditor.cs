using UnityEngine;
using UnityEditor;
public class MapEditor : EditorWindow
{
    private Texture2D mapTexture;
    private int[,] mapData;
    private int gridWidth = 32;
    private int gridHeight = 32;
    private Vector2 scrollPos;
    private bool isEditing = false;
    private int brushSize = 1;
    private int brushType = 0;
    private Color[] brushColors = { Color.white, Color.black, Color.gray };
    [MenuItem("Tools/Map Editor")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(MapEditor));
    }
    void OnGUI()
    {
        GUILayout.Label("Map Editor", EditorStyles.boldLabel);
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("New Map"))
        {
            CreateNewMap();
        }
        if (GUILayout.Button("Load Map"))
        {
            LoadMap();
        }
        if (GUILayout.Button("Save Map"))
        {
            SaveMap();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        if (mapTexture)
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            Rect textureRect = GUILayoutUtility.GetRect(mapTexture.width, mapTexture.height, GUILayout.ExpandWidth(false));
            EditorGUI.DrawTextureTransparent(textureRect, mapTexture);
            HandleInput(textureRect);
            GUILayout.EndScrollView();
        }
        GUILayout.Space(10);
        GUILayout.Label("Brush Settings", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        brushType = GUILayout.Toolbar(brushType, new string[] { "Walkable", "Unwalkable", "Mask" });
        brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 10);
        GUILayout.EndHorizontal();
    }
    void CreateNewMap()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create New Map", "New Map", "png", "Please enter a file name to save the map to");
        if (path == "")
        {
            return;
        }
        int width = Mathf.RoundToInt(EditorGUILayout.FloatField("Map Width", 1024));
        int height = Mathf.RoundToInt(EditorGUILayout.FloatField("Map Height", 1024));
        mapTexture = new Texture2D(width, height);
        mapData = new int[width / gridWidth, height / gridHeight];
        for (int x = 0; x < mapData.GetLength(0); x++)
        {
            for (int y = 0; y < mapData.GetLength(1); y++)
            {
                mapData[x, y] = 0;
            }
        }
        SaveMapData(path);
    }
    void LoadMap()
    {
        string path = EditorUtility.OpenFilePanel("Load Map", Application.dataPath, "png");
        if (path == "")
        {
            return;
        }
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        mapTexture = new Texture2D(2, 2);
        mapTexture.LoadImage(bytes);
        mapData = new int[mapTexture.width / gridWidth, mapTexture.height / gridHeight];
        for (int x = 0; x < mapData.GetLength(0); x++)
        {
            for (int y = 0; y < mapData.GetLength(1); y++)
            {
                Color pixelColor = mapTexture.GetPixel(x * gridWidth, y * gridHeight);
                if (pixelColor == Color.white)
                {
                    mapData[x, y] = 0;
                }
                else if (pixelColor == Color.black)
                {
                    mapData[x, y] = 1;
                }
                else if (pixelColor == Color.gray)
                {
                    mapData[x, y] = 2;
                }
            }
        }
    }
    void SaveMap()
    {
        if (mapTexture)
        {
            string path = EditorUtility.SaveFilePanel("Save Map", Application.dataPath, "New Map", "png");
            if (path != "")
            {
                SaveMapData(path);
            }
        }
    }
    void SaveMapData(string path)
    {
        for (int x = 0; x < mapData.GetLength(0); x++)
        {
            for (int y = 0; y < mapData.GetLength(1); y++)
            {
                Color pixelColor = brushColors[mapData[x, y]];
                for (int i = 0; i < gridWidth; i++)
                {
                    for (int j = 0; j < gridHeight; j++)
                    {
                        mapTexture.SetPixel(x * gridWidth + i, y * gridHeight + j, pixelColor);
                    }
                }
            }
        }
        byte[] bytes = mapTexture.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();
    }
    void HandleInput(Rect textureRect)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && textureRect.Contains(e.mousePosition))
        {
            isEditing = true;
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            isEditing = false;
        }
        if (isEditing)
        {
            int x = (int)(e.mousePosition.x - textureRect.x) / gridWidth;
            int y = (int)(e.mousePosition.y - textureRect.y) / gridHeight;
            for (int i = -brushSize + 1; i <= brushSize - 1; i++)
            {
                for (int j = -brushSize + 1; j <= brushSize - 1; j++)
                {
                    if (x + i >= 0 && x + i < mapData.GetLength(0) && y + j >= 0 && y + j < mapData.GetLength(1))
                    {
                        mapData[x + i, y + j] = brushType;
                    }
                }
            }
            e.Use();
        }
    }
}