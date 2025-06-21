using Godot;

namespace TerraBrush;

[Tool]
public partial class BrushDecal : Node3D {
    [NodePath] private Decal _decal;

    public override void _Ready() {
        base._Ready();
        this.RegisterNodePaths();
    }

    public void SetSize(int size) {
        _decal.Size = new Vector3(size, 1000, size);
    }

    public void SetRotation(float degrees)
    {
        Rotation = new Vector3(0, Mathf.DegToRad(degrees), 0);
    }

    public void SetBrushImage(Image image) {
        var imageCopy = new Image();
        imageCopy.CopyFrom(image);

        // Convert brush image to white with alpha for decal display
        for (var x = 0; x < imageCopy.GetWidth(); x++) {
            for (var y = 0; y < imageCopy.GetHeight(); y++) {
                var currentPixel = imageCopy.GetPixel(x, y);
                imageCopy.SetPixel(x, y, new Color(1, 1, 1, currentPixel.A));
            }
        }

        _decal.TextureAlbedo = ImageTexture.CreateFromImage(imageCopy);
        _decal.Modulate = (Color) ProjectSettings.GetSetting(SettingContants.DecalColor);
    }

    // New method to set both image and rotation simultaneously
    public void SetBrushImageAndRotation(Image image, float rotationDegrees) {
        SetBrushImage(image);
        SetRotation(rotationDegrees);
    }
}