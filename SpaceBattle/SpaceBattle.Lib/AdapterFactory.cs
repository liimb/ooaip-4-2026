using System;

namespace SpaceBattle.Lib
{
    public class AdapterFactory
    {
        private readonly Func<object, object> _createFunc;

        public AdapterFactory(Func<object, object> createFunc)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
        }

        public object Create(object entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Entity (словарь) не может быть null.");
            }

            return _createFunc(entity);
        }
    }
}
