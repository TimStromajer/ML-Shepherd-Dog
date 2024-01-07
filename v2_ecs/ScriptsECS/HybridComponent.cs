using UnityEngine;
using Unity.Entities;

public class HybridComponent : MonoBehaviour, IConvertGameObjectToEntity
{
    public Animator Animator;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        conversionSystem.AddHybridComponent(Animator);
    }
}
