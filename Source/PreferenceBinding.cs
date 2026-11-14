using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Amp
{
    // Editors refer to actual model fields, so a field rename is checked by the compiler.
    // Reflection is resolved once when the editor is built, outside the render loop.
    internal sealed class PreferenceBinding
    {
        readonly FieldInfo field;
        PreferenceBinding(FieldInfo field)
        {
            this.field = field;
        }

        public static PreferenceBinding For(Expression<Func<Preferences, object>> selector)
        {
            if (selector == null)
                throw new ArgumentNullException("selector");
            Expression body = selector.Body;
            var conversion = body as UnaryExpression;
            if (conversion != null && conversion.NodeType == ExpressionType.Convert)
                body = conversion.Operand;
            var member = body as MemberExpression;
            var field = member == null ? null : member.Member as FieldInfo;
            if (field == null || field.DeclaringType != typeof(Preferences) || member.Expression != selector.Parameters[0])
                throw new ArgumentException("Select a field directly from the preferences parameter.", "selector");
            return new PreferenceBinding(field);
        }

        public object Get(Preferences preferences)
        {
            return field.GetValue(preferences);
        }

        public void Set(Preferences preferences, object value)
        {
            field.SetValue(preferences, Convert.ChangeType(value, field.FieldType));
        }

        public void Reset(Preferences preferences)
        {
            field.SetValue(preferences, field.GetValue(new Preferences()));
        }
    }
}
