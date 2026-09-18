namespace ShapeCastle.Characters
{
    /// <summary>
    /// Supplies the character's configured walking speed to systems such as
    /// weapon throwing without coupling them to player or AI controllers.
    /// </summary>
    public interface IMovementSpeedProvider
    {
        float MoveSpeed { get; }
    }
}
