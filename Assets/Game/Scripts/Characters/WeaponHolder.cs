using ShapeCastle.Weapons;
using UnityEngine;

namespace ShapeCastle.Characters
{
    /// <summary>
    /// Records whether a character currently owns a weapon. Human players and
    /// AI characters can share this component and the same pickup rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponHolder : MonoBehaviour
    {
        [SerializeField] private MeleeWeaponController equippedWeapon;

        public bool HasWeapon => equippedWeapon != null;
        public MeleeWeaponController EquippedWeapon => equippedWeapon;
        public Transform WeaponSocket => transform;

        private void Awake()
        {
            if (equippedWeapon == null)
            {
                equippedWeapon = GetComponentInChildren<MeleeWeaponController>(true);
            }
        }

        public bool TryClaim(MeleeWeaponController weapon)
        {
            if (weapon == null || (equippedWeapon != null && equippedWeapon != weapon))
            {
                return false;
            }

            equippedWeapon = weapon;
            return true;
        }

        public void Release(MeleeWeaponController weapon)
        {
            if (equippedWeapon == weapon)
            {
                equippedWeapon = null;
            }
        }
    }
}
