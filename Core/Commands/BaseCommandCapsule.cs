using SpaceClaim.Api.V252.Extensibility;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands
{
    /// <summary>
    /// 모든 커맨드의 기본 클래스
    /// 다국어 지원 및 공통 기능 제공
    /// </summary>
    public abstract class BaseCommandCapsule : CommandCapsule
    {
        protected BaseCommandCapsule(string commandName, string text, System.Drawing.Image image, string hint = null)
            : base(commandName, text, image, hint)
        {
        }

        /// <summary>
        /// 커맨드 초기화 시 호출
        /// </summary>
        protected override void OnInitialize(Command command)
        {
            base.OnInitialize(command);
        }

        /// <summary>
        /// Active Window가 있는지 확인하는 헬퍼 메서드
        /// </summary>
        protected bool IsWindowActive()
        {
            return Window.ActiveWindow != null;
        }

        /// <summary>
        /// 현재 Document를 가져오는 헬퍼 메서드
        /// </summary>
        protected Document GetActiveDocument()
        {
            return Window.ActiveWindow?.Document;
        }

        /// <summary>
        /// 현재 Part를 가져오는 헬퍼 메서드
        /// </summary>
        protected Part GetActivePart()
        {
            return Window.ActiveWindow?.Document?.MainPart;
        }
    }
}
