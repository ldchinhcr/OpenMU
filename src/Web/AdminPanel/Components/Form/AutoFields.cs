// <copyright file="AutoFields.cs" company="MUnique">
// Được cấp phép theo Giấy phép MIT. Xem tệp LICENSE trong thư mục gốc của dự án để biết thông tin giấy phép đầy đủ.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components.Form;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.Web.AdminPanel.ComponentBuilders;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Một thành phần razor tự động tạo các trường nhập liệu cho tất cả các thuộc tính của kiểu mô hình biểu mẫu bao quanh.
/// Phải được sử dụng bên trong một <see cref="EditForm"/>.
/// </summary>
public class AutoFields : ComponentBase
{
    private static readonly IList<IComponentBuilder> Builders = new List<IComponentBuilder>();

    /// <summary>
    /// Khởi tạo các thành viên tĩnh của lớp <see cref="AutoFields"/>.
    /// </summary>
    static AutoFields()
    {
        Builders.Add(new PasswordHashFieldBuilder());
        Builders.Add(new TextFieldBuilder());
        Builders.Add(new NumberFieldBuilder<long>());
        Builders.Add(new NumberFieldBuilder<int>());
        Builders.Add(new NumberFieldBuilder<decimal>());
        Builders.Add(new NumberFieldBuilder<double>());
        Builders.Add(new NumberFieldBuilder<float>());
        Builders.Add(new ByteFieldBuilder());
        Builders.Add(new ShortFieldBuilder());
        Builders.Add(new BooleanFieldBuilder());
        Builders.Add(new DateTimeFieldBuilder());
        Builders.Add(new DateOnlyFieldBuilder());
        Builders.Add(new TimeOnlyFieldBuilder());
        Builders.Add(new TimeSpanFieldBuilder());
        Builders.Add(new EnumFieldBuilder());
        Builders.Add(new FlagsEnumFieldBuilder());
        Builders.Add(new ExitGateFieldBuilder());
        Builders.Add(new ItemStorageFieldBuilder());
        Builders.Add(new LookupFieldBuilder());
        Builders.Add(new EmbeddedFormFieldBuilder());
        Builders.Add(new ObjectCollectionFieldBuilder());
        Builders.Add(new IntCollectionFieldBuilder());
        Builders.Add(new ByteArrayFieldBuilder());
        Builders.Add(new ValueListFieldBuilder());
    }

    /// <summary>
    /// Lấy hoặc thiết lập ngữ cảnh của <see cref="EditForm"/>.
    /// </summary>
    /// <value>
    /// Ngữ cảnh.
    /// </value>
    [CascadingParameter]
    public EditContext Context { get; set; } = null!;

    /// <summary>
    /// Lấy hoặc thiết lập giá trị cho biết có ẩn các bộ sưu tập hay không.
    /// </summary>
    [Parameter]
    public bool HideCollections { get; set; }

    /// <summary>
    /// Lấy các thuộc tính sẽ được hiển thị trong thành phần này.
    /// </summary>
    /// <returns>Các thuộc tính sẽ được hiển thị trong thành phần này.</returns>
    protected virtual IEnumerable<PropertyInfo> Properties
    {
        get
        {
            if (this.Context?.Model is null)
            {
                this.Logger.LogError(this.Context is null ? "Ngữ cảnh là null" : "Mô hình là null");
                return Enumerable.Empty<PropertyInfo>();
            }

            try
            {
                return this.Context.Model.GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(p => p.GetCustomAttribute<TransientAttribute>() is null)
                    .Where(p => p.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)
                    .Where(p => !p.Name.StartsWith("Raw", StringComparison.InvariantCulture))
                    .Where(p => !p.Name.StartsWith("Joined", StringComparison.InvariantCulture))
                    .Where(p => !p.GetIndexParameters().Any())
                    .Where(p => !this.HideCollections || !p.PropertyType.IsGenericType)
                    .OrderBy(p => p.GetCustomAttribute<DisplayAttribute>()?.GetOrder())
                    .ThenByDescending(p => p.PropertyType == typeof(string))
                    .ThenByDescending(p => p.PropertyType.IsValueType)
                    .ThenByDescending(p => !p.PropertyType.IsGenericType)
                    .ToList();
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, $"Lỗi trong việc xác định các thuộc tính của kiểu {this.Context.Model.GetType()}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }

            return Enumerable.Empty<PropertyInfo>();
        }
    }

    /// <summary>
    /// Lấy hoặc thiết lập dịch vụ thông báo.
    /// </summary>
    /// <value>
    /// Dịch vụ thông báo.
    /// </value>
    [Inject]
    private IChangeNotificationService NotificationService { get; set; } = null!;

    [Inject]
    private ILogger<AutoFields> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        int i = 0;
        foreach (var propertyInfo in this.Properties)
        {
            IComponentBuilder? componentBuilder = null;
            try
            {
                componentBuilder = Builders.FirstOrDefault(b => b.CanBuildComponent(propertyInfo));
                if (componentBuilder != null)
                {
                    // TODO: Xây dựng một cái gì đó xung quanh các nhóm (cùng DisplayAttribute.GroupName)
                    i = componentBuilder.BuildComponent(this.Context.Model, propertyInfo, builder, i, this.NotificationService);
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, $"Lỗi khi xây dựng thành phần cho thuộc tính {this.Context.Model.GetType().Name}.{propertyInfo.Name} với bộ xây dựng thành phần {componentBuilder}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
        }
    }
}