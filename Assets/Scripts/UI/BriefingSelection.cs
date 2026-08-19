// MissionSelectManager에서 클릭된 미션 정보를 Briefing_Room 씬으로 넘기는 static 홀더.
// SceneManager.LoadScene은 씬을 통째로 갈아끼우므로 DontDestroyOnLoad 오브젝트 없이도
// static 필드는 그대로 유지된다 (doc/0616).
public static class BriefingSelection
{
    public static int MissionNumber;
    public static bool IsSubMission;
    public static string TargetSceneName;
}
