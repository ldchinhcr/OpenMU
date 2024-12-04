// <copyright file="FlagsEnumField.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components.Form;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Trường tìm kiếm cho phép chọn nhiều đối tượng sẽ được lưu trữ trong một <see cref="IList{TObject}"/> đã liên kết.
/// </summary>
public partial class FlagsEnumField<TValue> : NotifyableInputBase<TValue>
    where TValue : struct, Enum
{
    private static readonly TValue[] PossibleFlags = Enum.GetValues(typeof(TValue)).OfType<TValue>().Where(v => !default(TValue).HasFlag(v)).OrderBy(v => v.ToString()).ToArray();

    /// <summary>
    /// Lấy hoặc thiết lập nhãn sẽ được hiển thị. Nếu không được cung cấp rõ ràng, thành phần sẽ hiển thị
    /// Tên được định nghĩa trong <see cref="DisplayAttribute"/>. Nếu không có Tên trong <see cref="DisplayAttribute"/>, nó sẽ hiển thị tên thuộc tính thay thế.
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    private string Placeholder => this.UnassignedFlags.Any() ? "Thêm ..." : "Không còn available nào nữa";

    private IEnumerable<TValue> UnassignedFlags => PossibleFlags.Where(f => !this.Value.HasFlag(f));

    private IList<TValue> ValueAsSingleFlags
    {
        get
        {
            return PossibleFlags.Where(possible => this.Value.HasFlag(possible)).ToList();
        }
    }

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Được gọi khi các cờ đã chọn thay đổi.
    /// Kết hợp các cờ thành một giá trị của thành phần này.
    /// </summary>
    /// <param name="flags">Các cờ đã chọn.</param>
    private async Task OnValueChangedAsync(IList<TValue> flags)
    {
        if (flags.Count == 0)
        {
            this.CurrentValue = default;
        }
        else
        {
            var result = flags.Cast<int>().Aggregate((a, b) => a | b);
            this.CurrentValue = (TValue)(object)result;
        }
    }

    /// <summary>
    /// Tìm kiếm các cờ có sẵn, chưa được gán cho giá trị.
    /// </summary>
    /// <param name="text">Văn bản tìm kiếm.</param>
    /// <returns>Các cờ có sẵn.</returns>
    private async Task<IEnumerable<TValue>> SearchAsync(string text)
    {
        return this.UnassignedFlags.Where(f => f.ToString().Contains(text, StringComparison.InvariantCultureIgnoreCase));
    }
}