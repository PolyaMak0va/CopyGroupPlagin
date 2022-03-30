using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyGroupPlagin
{
    // сначала добавляем библиотеки Revit RevitAPI.dll и RevitAPIUI.dll
    // поскольку мы хотим, чтобы наше приложение работало как внешняя команда, надо реализовать интерфейс IExternalCommand.
    [TransactionAttribute(TransactionMode.Manual)]
    public class CopyGroup : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // первым делом нужно добраться до документа
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document; // содержит базу данных элементов внутри открытого документа

            // попросим у пользователя выбрать группу для копирования
            // результатом этого метода будет объект Reference
            Reference reference = uidoc.Selection.PickObject(ObjectType.Element, "Выберите группу объектов");

            // получили ссылку на выбранные пользователем объекты. Ссылка на объекты - это не сам объект, это что-то идентификатора объекта.
            // если мы хотим скопировать, изменить, то нам нужен доступ к самому объекту.
            Element element = doc.GetElement(reference);

            // Element - это родительский или базовый класс для всех элементов в Revit API
            // преобразовываем объект из базового типа в тип Group. Преобразовывать можно при помощи приведения в круглых скобках либо, что лучше, с помощью as
            Group group = element as Group;

            // попросим у пользователя выбрать точку, это будет комната, в кот-ую мы хотим скопировать объект
            XYZ point = uidoc.Selection.PickPoint("Выберите точку");

            // последняя часть - вставка группы в заданную точку
            // поскольку это приведёт к изменению модели, необходимо воспользоваться объектом транзакции
            Transaction transaction = new Transaction(doc);
            transaction.Start("Копирование группы объектов");
            // все действия, кот-е касаются создания чего-л. в док-те ревит, будут относиться к пространству имён Autodesk.Revit.Creation
            doc.Create.PlaceGroup(point, group.GroupType);

            transaction.Commit();

            return Result.Succeeded;
        }
    }
}
