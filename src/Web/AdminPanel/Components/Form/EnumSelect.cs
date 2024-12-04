// <copyright file="EnumSelect.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components.Form;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

/// <summary>
/// Một thành phần chọn dropdown cho các giá trị enum của <typeparamref name="TValue" />.
/// </summary>
/// <typeparam name="TValue">Loại của enum.</typeparam>
public class EnumSelect<TValue> : NotifyableInputBase<TValue>
{
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        int i = 0;
        builder.OpenElement(i++, "select");
        builder.AddMultipleAttributes(i++, this.AdditionalAttributes);
        builder.AddAttribute(i++, "class", this.CssClass);
        builder.AddAttribute(i++, "id", this.FieldIdentifier.FieldName);
        builder.AddAttribute(i++, "value", this.CurrentValueAsString);
        builder.AddAttribute(i++, "onchange", EventCallback.Factory.CreateBinder<string>(this, value => this.CurrentValueAsString = value, this.CurrentValueAsString ?? string.Empty));

        foreach (var enumValue in Enum.GetValues(typeof(TValue)))
        {
            var name = Enum.GetName(typeof(TValue), enumValue);
            var displayName = typeof(TValue).GetField(name!)!.GetCustomAttribute<DisplayAttribute>()?.Name;
            builder.OpenElement(i++, "option");
            builder.AddAttribute(i++, "value", enumValue.ToString());
            if (this.CurrentValueAsString == enumValue.ToString())
            {
                builder.AddAttribute(i++, "selected", true);
            }

            builder.AddContent(i++, displayName ?? name);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, [MaybeNullWhen(false)] out TValue result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (!typeof(TValue).IsEnum)
        {
            throw new InvalidOperationException($"{this.GetType()} không hỗ trợ loại '{typeof(TValue)}'.");
        }

        if (BindConverter.TryConvertTo<TValue>(value, CultureInfo.CurrentCulture, out var parsedValue))
        {
            result = parsedValue;
            validationErrorMessage = null;
            return true;
        }

        result = default;
        validationErrorMessage = $"Trường {this.FieldIdentifier.FieldName} không hợp lệ.";
        return false;
    }
}