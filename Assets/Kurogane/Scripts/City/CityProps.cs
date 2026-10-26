namespace Kurogane.City
{
    /// <summary>
    /// Celdas del atlas del pueblo (ver ProceduralTextures.TownAtlas). Los componentes del pueblo (Blinker,
    /// CrossingController) van cada uno en su archivo: Unity solo guarda en la escena un componente si su clase
    /// se llama como el archivo, y el pueblo se hornea en la escena.
    /// </summary>
    public enum TownCell
    {
        WallCream, WallWhite, WallBeige, WallBlue, Wood, Roof, WindowLit, WindowDark,
        Shutter, KonbiniWall, KonbiniStripe, KonbiniGlass, Concrete, Asphalt, Sidewalk, MetalDark,
        Noren, VendingRed, VendingBlue, VendingWhite, SignRed, SignBlue, CanopyLight, Paddy,
        Dyke, RailTie, RailSteel, Stripes, Housing, Door, Balcony, AcUnit,
        PhoneBooth, Cedar, Trunk, Ballast, Paint, SignWood, Lantern, StreetLamp,
    }
}
