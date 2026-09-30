namespace IdleMart.World
{
    /// <summary>Scene object the player can click. The UI decides what to show for it.</summary>
    public interface IClickable
    {
        /// <summary>Called on a direct click, before the UI opens its panel.</summary>
        void OnClicked();
    }
}
