using System;
using System.Drawing;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs;

#if V251
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.BendingFixture
{
    /// <summary>
    /// 기존 바디에 3점 벤딩 지지구조 적용 커맨드
    /// </summary>
    public class ApplyBendingFixtureCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ApplyBendingFixture";

        public ApplyBendingFixtureCommand()
            : base(CommandName, "벤딩 지그", IconHelper.BendingFixtureIcon,
                   "기존 모델에 3점 벤딩 지지구조를 적용합니다")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = IsWindowActive();
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            try
            {
                Part part = GetActivePart();

                if (part == null)
                {
                    ValidationHelper.ShowError("활성 Part가 없습니다.", "오류");
                    return;
                }

                // 사전 선택된 바디 감지
                DesignBody preSelectedBody = TryGetPreSelectedBody();

                var dialog = new ApplyBendingFixtureDialog(part, preSelectedBody);
                dialog.Show();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"벤딩 지그 적용 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류"
                );
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }

        /// <summary>
        /// SpaceClaim 뷰에서 사전 선택된 바디를 가져오기 시도
        /// </summary>
        private DesignBody TryGetPreSelectedBody()
        {
            try
            {
                var window = Window.ActiveWindow;
                if (window == null) return null;

                var selection = window.ActiveContext.SingleSelection;

                if (selection is DesignBody db)
                    return db;

                if (selection is DesignFace df)
                    return df.Parent as DesignBody;

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
