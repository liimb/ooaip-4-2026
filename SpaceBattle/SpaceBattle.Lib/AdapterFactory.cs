using System;

namespace SpaceBattle.Lib
{
    // Это и есть шаблон фабрики (Factory Method)
    public class AdapterFactory
    {
        private readonly Func<object, object> _createFunc;

        // Конструктор принимает делегат, который умеет создавать объект
        public AdapterFactory(Func<object, object> createFunc)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
        }

        // Главный метод фабрики: берет entity (словарь) и возвращает готовый адаптер
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
