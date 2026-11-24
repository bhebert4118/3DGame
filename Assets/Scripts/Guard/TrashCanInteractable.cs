using UnityEngine;

/// <summary>
/// The guard-room trash can. Pressing E on it hands the player a piece of rubbish to throw
/// (see <see cref="PlayerThrow"/>). The player can carry one at a time and come back for more.
/// Sits on an invisible box placed over the bin prop, like the alarm computer's click target.
/// </summary>
public class TrashCanInteractable : Interactable
{
    protected override void OnInteract()
    {
        PlayerThrow thrower = FindFirstObjectByType<PlayerThrow>();
        if (thrower != null) thrower.PickUp();
        else Debug.LogWarning("[TrashCan] No PlayerThrow on the player, so there is nobody to hand the rubbish to.", this);
    }
}
