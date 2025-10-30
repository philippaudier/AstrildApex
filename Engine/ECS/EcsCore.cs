namespace Engine.ECS;

public readonly record struct Entity(int Id);

public interface IComponent;

public interface ISystem
{
    void Update(float dt);
}

public sealed class World
{
    private int _nextId = 1;
    public Entity Create() => new Entity(_nextId++);
}
