namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     One file under
///     <c>
///         templates/mobiles/
///     </c>
///     : a
///     <c>
///         [[mobile]]
///     </c>
///     array of <see cref="MobileTemplate" />.
/// </summary>
public class MobileTemplateFile
{
    /// <summary>
    ///     The templates in the file.
    /// </summary>
    public List<MobileTemplate> Mobile { get; set; } = [];
}
