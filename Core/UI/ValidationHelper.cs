using System;
using System.Windows.Forms;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI
{
    /// <summary>
    /// 사용자 입력 검증을 위한 헬퍼 클래스
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// 양수 값 검증
        /// </summary>
        public static bool ValidatePositive(decimal value, string fieldName, out string errorMessage)
        {
            if (value <= 0)
            {
                errorMessage = $"{fieldName}은(는) 0보다 커야 합니다.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 범위 검증
        /// </summary>
        public static bool ValidateRange(decimal value, decimal min, decimal max, string fieldName, out string errorMessage)
        {
            if (value < min || value > max)
            {
                errorMessage = $"{fieldName}은(는) {min}에서 {max} 사이여야 합니다.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 비어있지 않은 문자열 검증
        /// </summary>
        public static bool ValidateNotEmpty(string value, string fieldName, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = $"{fieldName}을(를) 입력해주세요.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 오류 메시지 표시
        /// </summary>
        public static void ShowError(string message, string title = "입력 오류")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// 정보 메시지 표시
        /// </summary>
        public static void ShowInfo(string message, string title = "정보")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 확인 메시지 표시
        /// </summary>
        public static bool ShowConfirm(string message, string title = "확인")
        {
            return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
    }
}
