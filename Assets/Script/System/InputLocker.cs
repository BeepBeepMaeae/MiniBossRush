// InputLocker.cs
public static class InputLocker
{
    public static bool CanMove = false;
    public static bool CanJump = false;
    public static bool CanDash = false;
    public static bool CanDodge = false;
    public static bool CanSwitchWeapon = false;
    public static bool CanAttack = false;
    public static bool CanUseItem = false;
    

    public static void DisableAll()
    {
        CanMove = CanJump = CanDash = CanSwitchWeapon = CanAttack = CanUseItem = CanDodge = false;
    }
}
