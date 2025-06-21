using System;
using System.Threading.Tasks;
using Godot;
namespace TerraBrush;

#if TOOLS
public enum TerrainToolType {
    None = 0,
    [ToolType(typeof(SculptTool))] TerrainAdd = 1,
    [ToolType(typeof(SculptTool))] TerrainRemove = 2,
    [ToolType(typeof(SculptTool))] TerrainSmooth = 3,
    [ToolType(typeof(SculptTool))] TerrainFlatten = 4,
    [ToolType(typeof(SetHeightTool))] TerrainSetHeight = 5,
    [ToolType(typeof(SetAngleTool))] TerrainSetAngle = 19,
    [ToolType(typeof(TextureTool))] Paint = 6,
    [ToolType(typeof(FoliageTool))] FoliageAdd = 7,
    [ToolType(typeof(FoliageTool))] FoliageRemove = 8,
    [ToolType(typeof(ObjectTool))] ObjectAdd = 9,
    [ToolType(typeof(ObjectTool))] ObjectRemove = 10,
    [ToolType(typeof(WaterTool))] WaterAdd = 11,
    [ToolType(typeof(WaterTool))] WaterRemove = 12,
    [ToolType(typeof(WaterFlowTool))] WaterFlowAdd = 13,
    [ToolType(typeof(WaterFlowTool))] WaterFlowRemove = 14,
    [ToolType(typeof(SnowTool))] SnowAdd = 15,
    [ToolType(typeof(SnowTool))] SnowRemove = 16,
    [ToolType(typeof(HoleTool))] HoleAdd = 17,
    [ToolType(typeof(HoleTool))] HoleRemove = 18,
    [ToolType(typeof(LockTool))] LockAdd = 20,
    [ToolType(typeof(LockTool))] LockRemove = 21,
}
#endif

public partial class TerraBrushTool : Node3D {
#if TOOLS
    private int _brushSize = 100;
    private Image _originalBrushImage;
    private Image _brushImage;
    private Image _rotatedBrushImage;
    private int? _selectedBrushIndex = null;
    private float _brushStrength = 0.1f;
    private float _selectedSetHeight = 0;
    private float _selectedSetAngle = 0;
    private Vector3? _selectedSetAngleInitialPoint = null;
    private int? _textureSetIndex = null;
    private int? _foliageIndex = null;
    private int? _objectIndex = null;
    private ToolBase _currentTool;
    private TerrainToolType _terrainTool = TerrainToolType.TerrainAdd;
    private float _brushRotationDegrees = 0f;
    private float _lastRotation = -999f;

    public TerrainToolType TerrainTool => _terrainTool;
    public ToolBase CurrentTool => _currentTool;
    public EditorUndoRedoManager UndoRedo { get;set; }

    public int BrushSize => _brushSize;
    public float BrushStrength => _brushStrength;
    public Image BrushImage => _brushImage;
    public int? SelectedBrushIndex => _selectedBrushIndex;
    public float SelectedSetHeight => _selectedSetHeight;
    public float SelectedSetAngle => _selectedSetAngle;
    public Vector3? SelectedSetAngleInitialPoint => _selectedSetAngleInitialPoint;
    public int? TextureSetIndex => _textureSetIndex;
    public int? FoliageIndex => _foliageIndex;
    public int? ObjectIndex => _objectIndex;
    public float BrushRotationDegrees => _brushRotationDegrees;

    [Export(PropertyHint.None, $"{ButtonInspectorPlugin.ButtonInspectorHintString}_{nameof(OnCreateTerrain)}")]
    public bool CreateTerrain {
        get {
            return false;
        } set {}
    }

    [Export(PropertyHint.None, $"{ButtonInspectorPlugin.ButtonInspectorHintString}_{nameof(OnUpdateTerrainSettings)}")]
    public bool UpdateTerrain {
        get {
            return false;
        } set {}
    }

    [Export(PropertyHint.None, $"{ButtonInspectorPlugin.ButtonInspectorHintString}_{nameof(OnRemoveTerrain)}")]
    public bool RemoveTerrain {
        get {
            return false;
        } set {}
    }

    [ExportGroup("Lock | Unlock")]
    [Export(PropertyHint.None, $"{ButtonInspectorPlugin.ButtonInspectorHintString}_{nameof(OnLockTerrain)}")]
    public bool LockAllTerrain {
        get {
            return false;
        } set {}
    }

    [ExportGroup("Lock | Unlock")]
    [Export(PropertyHint.None, $"{ButtonInspectorPlugin.ButtonInspectorHintString}_{nameof(OnUnlockTerrain)}")]
    public bool UnlockAllTerrain {
        get {
            return false;
        } set {}
    }

    [ExportGroup("Import | Export")]
    [Export(PropertyHint.None, $"{ButtonInspectorPlugin.ButtonInspectorHintString}_{nameof(OnImportTerrain)}")]
    public bool ImportTerrain {
        get {
            return false;
        } set {}
    }

    [ExportGroup("Import | Export")]
    [Export(PropertyHint.None, $"{ButtonInspectorPlugin.ButtonInspectorHintString}_{nameof(OnExportTerrain)}")]
    public bool ExportTerrain {
        get {
            return false;
        } set {}
    }

    public override void _Ready() {
        base._Ready();

        SetTerrainTool(_terrainTool);
    }

    public void SetTerrainTool(TerrainToolType terrainToolType) {
        _terrainTool = terrainToolType;

        var terrainToolTypeAttribute = AttributeUtils.GetAttribute<ToolTypeAttribute>(terrainToolType);
        if (terrainToolTypeAttribute == null) {
            _currentTool?.BeforeDeselect();
            _currentTool = null;
        } else if (_currentTool == null || _currentTool.GetType() != terrainToolTypeAttribute.PaintToolType) {
            _currentTool?.BeforeDeselect();
            _currentTool = (ToolBase) Activator.CreateInstance(terrainToolTypeAttribute.PaintToolType, this);
        }
    }

    public void BeingEditTerrain() {
        _currentTool?.BeginPaint();
    }

    public void EditTerrain(Vector3 meshPosition)
    {
        // Regenerate rotated brush if rotation changed or brush doesn't exist
        if (_rotatedBrushImage == null || _lastRotation != _brushRotationDegrees)
        {
            if (_originalBrushImage == null || _originalBrushImage.IsEmpty())
            {
                GD.PushError("Original brush image is null or empty.");
                return;
            }

            // Simple approach: resize first, then rotate with larger output size to prevent clipping
            var resizedBrush = new Image();
            resizedBrush.CopyFrom(_originalBrushImage);
            resizedBrush.Resize(_brushSize, _brushSize, Image.Interpolation.Lanczos);

            // Rotate with larger size output to prevent clipping
            _rotatedBrushImage = RotateImageSimple(resizedBrush, _brushRotationDegrees);
            _lastRotation = _brushRotationDegrees;

            if (_rotatedBrushImage == null || _rotatedBrushImage.IsEmpty())
            {
                GD.PushError("Rotated brush image is invalid.");
                return;
            }

            // Update the main brush image to match the rotated one
            _brushImage = new Image();
            _brushImage.CopyFrom(_rotatedBrushImage);

            GD.Print($"Painting with rotated brush: original_size={_brushSize} rotated_size={_rotatedBrushImage.GetWidth()} rotation={_brushRotationDegrees}");
        }

        // Convert mesh position to image coordinates
        var localPos = meshPosition - GlobalPosition;
        
        // Apply the same coordinate transformation used throughout TerraBrush
        var scaledX = (localPos.X + ZonesSize / 2f) * Resolution;
        var scaledZ = (localPos.Z + ZonesSize / 2f) * Resolution;
        var imagePosition = new Vector2(scaledX, scaledZ);

        // Use the actual size of the rotated brush (which may be larger than _brushSize)
        int actualBrushSize = _rotatedBrushImage.GetWidth();

        // Use the rotated brush for painting
        _currentTool?.Paint(
            _terrainTool,
            _rotatedBrushImage,
            actualBrushSize,  // Use the actual rotated brush size
            _brushStrength,
            imagePosition
        );
    }

    public void EndEditTerrain() {
        _currentTool?.EndPaint();
    }

    public void RotateBrush(float deltaDegrees)
    {
        _brushRotationDegrees = Mathf.PosMod(_brushRotationDegrees + deltaDegrees, 360f);
        
        // Force regeneration on next EditTerrain call
        _lastRotation = -999f;
    }

    public void SetBrushRotation(float degrees)
    {
        _brushRotationDegrees = Mathf.PosMod(degrees, 360f);
        
        // Force regeneration on next EditTerrain call
        _lastRotation = -999f;
    }

    private Image RotateImageSimple(Image src, float degrees)
    {
        if (Mathf.Abs(degrees) < 0.01f) // No rotation needed
        {
            var copy = new Image();
            copy.CopyFrom(src);
            return copy;
        }

        int srcSize = src.GetWidth(); // Assume square
        
        // Calculate the size needed to fit the entire rotated image
        float angleRad = Mathf.DegToRad(Mathf.Abs(degrees));
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);
        
        // Calculate diagonal length to ensure we can fit the rotated image
        float diagonal = srcSize * Mathf.Sqrt(2f);
        int newSize = Mathf.CeilToInt(diagonal);
        
        // Make sure it's at least as big as the source
        newSize = Mathf.Max(newSize, srcSize);
        
        // Create result with larger size to accommodate rotation
        Image result = Image.Create(newSize, newSize, false, src.GetFormat());
        result.Fill(new Color(0, 0, 0, 0));

        float rotAngleRad = Mathf.DegToRad(degrees);
        Vector2 srcCenter = new Vector2(srcSize / 2f, srcSize / 2f);
        Vector2 dstCenter = new Vector2(newSize / 2f, newSize / 2f);
        
        float rCos = Mathf.Cos(rotAngleRad); // Remove the negative to match decal direction
        float rSin = Mathf.Sin(rotAngleRad);

        for (int y = 0; y < newSize; y++)
        {
            for (int x = 0; x < newSize; x++)
            {
                Vector2 dstPos = new Vector2(x, y);
                Vector2 dstOffset = dstPos - dstCenter;
                
                // Rotate the offset to find source position
                Vector2 srcOffset = new Vector2(
                    dstOffset.X * rCos - dstOffset.Y * rSin,
                    dstOffset.X * rSin + dstOffset.Y * rCos
                );
                
                Vector2 srcPos = srcCenter + srcOffset;
                
                int srcX = Mathf.RoundToInt(srcPos.X);
                int srcY = Mathf.RoundToInt(srcPos.Y);
                
                // Simple bounds check and copy
                if (srcX >= 0 && srcY >= 0 && srcX < srcSize && srcY < srcSize)
                {
                    result.SetPixel(x, y, src.GetPixel(srcX, srcY));
                }
            }
        }

        return result;
    }

    private Image RotateImage(Image src, float degrees)
    {
        if (Mathf.Abs(degrees) < 0.01f) // No rotation needed
        {
            var copy = new Image();
            copy.CopyFrom(src);
            return copy;
        }

        int srcSize = src.GetWidth(); // Assuming square brush
        
        // Calculate the size needed to fit the rotated image without clipping
        float angleRad = Mathf.DegToRad(Mathf.Abs(degrees));
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);
        
        // Calculate the bounding box size for the rotated image
        float newSize = srcSize * (cos + sin);
        int rotatedSize = Mathf.CeilToInt(newSize);
        
        // Ensure the rotated size is not smaller than original (for small angles)
        rotatedSize = Mathf.Max(rotatedSize, srcSize);
        
        // Create the larger canvas for the rotated image
        Image result = Image.Create(rotatedSize, rotatedSize, false, src.GetFormat());
        result.Fill(new Color(0, 0, 0, 0)); // transparent background

        float rotAngleRad = Mathf.DegToRad(degrees);
        Vector2 srcCenter = new Vector2(srcSize / 2f, srcSize / 2f);
        Vector2 dstCenter = new Vector2(rotatedSize / 2f, rotatedSize / 2f);

        // Use backward rotation mapping for better quality
        float rCos = Mathf.Cos(-rotAngleRad); // Negative for proper rotation direction
        float rSin = Mathf.Sin(-rotAngleRad);

        for (int dstY = 0; dstY < rotatedSize; dstY++)
        {
            for (int dstX = 0; dstX < rotatedSize; dstX++)
            {
                // Map destination pixel back to source
                Vector2 dstOffset = new Vector2(dstX, dstY) - dstCenter;
                
                Vector2 srcOffset = new Vector2(
                    dstOffset.X * rCos - dstOffset.Y * rSin,
                    dstOffset.X * rSin + dstOffset.Y * rCos
                );

                Vector2 srcPos = srcCenter + srcOffset;

                // Check if the source position is within bounds
                if (srcPos.X >= 0 && srcPos.Y >= 0 && srcPos.X < srcSize && srcPos.Y < srcSize)
                {
                    // Bilinear interpolation for smoother results
                    int srcX = Mathf.FloorToInt(srcPos.X);
                    int srcY = Mathf.FloorToInt(srcPos.Y);

                    if (srcX < srcSize - 1 && srcY < srcSize - 1)
                    {
                        float fracX = srcPos.X - srcX;
                        float fracY = srcPos.Y - srcY;

                        Color c00 = src.GetPixel(srcX, srcY);
                        Color c10 = src.GetPixel(srcX + 1, srcY);
                        Color c01 = src.GetPixel(srcX, srcY + 1);
                        Color c11 = src.GetPixel(srcX + 1, srcY + 1);

                        Color c0 = c00.Lerp(c10, fracX);
                        Color c1 = c01.Lerp(c11, fracX);
                        Color finalColor = c0.Lerp(c1, fracY);

                        result.SetPixel(dstX, dstY, finalColor);
                    }
                    else
                    {
                        // Fallback to nearest neighbor for edge pixels
                        result.SetPixel(dstX, dstY, src.GetPixel(srcX, srcY));
                    }
                }
            }
        }

        return result;
    }

    public void SetCurrentBrush(int brushIndex, Image brushImage, float rotationDegrees = 0)
    {
        _selectedBrushIndex = brushIndex;
        _originalBrushImage = new Image();
        _originalBrushImage.CopyFrom(brushImage);

        // Set initial rotation if provided
        if (Mathf.Abs(rotationDegrees) > 0.01f)
        {
            _brushRotationDegrees = Mathf.PosMod(rotationDegrees, 360f);
        }

        // Force brush regeneration
        _lastRotation = -999f;
        
        // Update brush size to trigger regeneration with current settings
        SetBrushSize(_brushSize);
    }

    public void SetBrushSize(int value) {
        _brushSize = value;
        
        // Force regeneration of rotated brush with new size
        _lastRotation = -999f;
        
        // Update the base brush image for immediate feedback
        if (_originalBrushImage != null && !_originalBrushImage.IsEmpty())
        {
            _brushImage = new Image();
            _brushImage.CopyFrom(_originalBrushImage);
            _brushImage.Resize(value, value, Image.Interpolation.Lanczos);
        }
    }

    public void SetBrushStrength(float value) {
        _brushStrength = value;
    }

    public void SetTextureSet(int? textureSetIndex) {
        _textureSetIndex = textureSetIndex;
    }

    public void SetFoliage(int? foliageIndex) {
        _foliageIndex = foliageIndex;
    }

    public void SetObject(int? objectIndex) {
        _objectIndex = objectIndex;
    }

    public void UpdateSetHeightValue(float value) {
        _selectedSetHeight = value;
    }

    public void UpdateSetAngleValue(float value, Vector3? initialPoint) {
        _selectedSetAngle = value;
        _selectedSetAngleInitialPoint = initialPoint;
    }

    public async Task OnImportTerrain() {
        var settings = await DialogUtils.ShowImportDialog(GetParent(), this);
        if (settings != null) {
            ImporterEngine.ImportTerrain(this, settings);
            OnUpdateTerrainSettings();
        }
    }

    public async Task OnExportTerrain() {
        var folder = await DialogUtils.ShowFileDialog(GetTree().Root, fileMode: EditorFileDialog.FileModeEnum.OpenDir);
        if (string.IsNullOrWhiteSpace(folder)) {
            return;
        }

        ExporterEngine.ExportTerrain(this, folder);
    }
#endif

#region  " Virtual overrides "
    public virtual int ZonesSize { get;set; }
    public virtual int Resolution { get;set; }
    public virtual string DataPath { get;set; }
    public virtual ZonesResource TerrainZones { get;set; }
    public virtual TextureSetsResource TextureSets { get;set; }
    public virtual FoliageResource[] Foliages { get;set; }
    public virtual ObjectResource[] Objects { get;set; }
    public virtual WaterResource WaterDefinition { get;set; }
    public virtual SnowResource SnowDefinition { get;set; }
    public virtual void OnCreateTerrain() {}
    public virtual void OnUpdateTerrainSettings() {}
    public virtual void OnRemoveTerrain() {}
    public virtual void OnLockTerrain() {}
    public virtual void OnUnlockTerrain() {}
#endregion
}