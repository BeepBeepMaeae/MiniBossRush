// Assets/Script/System/Save/SaveLoadBuffer.cs
public static class SaveLoadBuffer
{
    // 다음 씬이 뜬 뒤 적용할 세이브 데이터
    public static SaveData Pending;

    public static void Clear() => Pending = null;
}
