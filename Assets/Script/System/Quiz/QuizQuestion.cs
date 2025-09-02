// QuizQuestion.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/Question")]
public class QuizQuestion : ScriptableObject
{
    [TextArea] public string questionText;    // 질문 문구
    public string[] options;                  // 선택지 배열
    public int correctIndex;                  // 정답 인덱스(0부터)
}
