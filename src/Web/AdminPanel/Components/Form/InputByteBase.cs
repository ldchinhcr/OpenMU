// <copyright file="InputByteBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components.Form;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

/// <summary>
/// Một thành phần đầu vào để chỉnh sửa các giá trị byte số.
/// </summary>
/// <typeparam name="TByte">Loại của byte.</typeparam>
public abstract class InputByteBase<TByte> : InputBase<TByte>
{
    /// <summary>
    /// Lấy hoặc thiết lập thông báo lỗi được sử dụng khi hiển thị lỗi phân tích cú pháp.
    /// </summary>
    [Parameter]
    public string ParsingErrorMessage { get; set; } = "Trường {0} phải là một số trong khoảng từ 0 đến 255.";

    /// <summary>
    /// Lấy hoặc thiết lập giá trị tối thiểu.
    /// </summary>
    [Parameter]
    public byte Min { get; set; } = byte.MinValue;

    /// <summary>
    /// Lấy hoặc thiết lập giá trị tối đa.
    /// </summary>
    [Parameter]
    public byte Max { get; set; } = byte.MaxValue;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "step", 1);
        builder.AddMultipleAttributes(2, this.AdditionalAttributes);
        builder.AddAttribute(3, "type", "number");
        builder.AddAttribute(4, "class", this.CssClass);
        builder.AddAttribute(5, "value", BindConverter.FormatValue(this.CurrentValueAsString));
        builder.AddAttribute(6, "onchange", EventCallback.Factory.CreateBinder<string>(this, v => this.CurrentValueAsString = v, this.CurrentValueAsString!));
        builder.AddAttribute(7, "min", this.Min);
        builder.AddAttribute(8, "max", this.Max);
        builder.CloseElement();
    }
}