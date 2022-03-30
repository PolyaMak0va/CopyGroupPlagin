using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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
            // весь блок, который может вызвать исключение - помещаем в блок try/catch
            try
            {
                // первым делом нужно добраться до документа
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document; // содержит базу данных элементов внутри открытого документа

                // попросим у пользователя выбрать группу для копирования
                // результатом этого метода будет объект Reference, добавив фильтр, который будет передавать экземпляр класса, унаследованного от ISelectionFilter
                GroupPickFilter pickFilter = new GroupPickFilter();
                Reference reference = uidoc.Selection.PickObject(ObjectType.Element, pickFilter, "Выберите группу объектов");

                // получили ссылку на выбранные пользователем объекты. Ссылка на объекты - это не сам объект, это что-то идентификатора объекта.
                // если мы хотим скопировать, изменить, то нам нужен доступ к самому объекту.
                Element element = doc.GetElement(reference);

                // Element - это родительский или базовый класс для всех элементов в Revit API
                // преобразовываем объект из базового типа в тип Group. Преобразовывать можно при помощи приведения в круглых скобках либо, что лучше, с помощью as
                Group group = element as Group;

                // напишем метод, кот-й по объекту вычисляет его центр на основе BoundingBox
                XYZ groupCenter = GetElementCenter(group);

                // теперь, имея центр, можно определить, в какую комнату попадает эта точка
                Room roomOut = GetRoomByPoint(doc, groupCenter);
                XYZ roomCenterOut = GetElementCenter(roomOut);

                // определяем смещение: центра группы относительно центра комнаты
                XYZ offset = groupCenter - roomCenterOut;

                // попросим у пользователя выбрать точку, это будет комната, в кот-ую мы хотим скопировать объект
                XYZ point = uidoc.Selection.PickPoint("Выберите точку");
                // находим центр комнаты входящей точки
                Room roomIn = GetRoomByPoint(doc, point);
                XYZ roomCenterIn = GetElementCenter(roomIn);
                // на основе смещения offset вычисляем точку, в которую необходимо выполнить вставку группы и вставляем её именно в эту рассчитанную точку
                XYZ pointOfCentrRoomIn = roomCenterIn + offset;

                // последняя часть - вставка группы в заданную точку
                // поскольку это приведёт к изменению модели, необходимо воспользоваться объектом транзакции
                Transaction transaction = new Transaction(doc);
                transaction.Start("Копирование группы объектов");
                // все действия, кот-е касаются создания чего-л. в док-те ревит, будут относиться к пространству имён Autodesk.Revit.Creation
                // вставляем группу объектов в центр комнаты, на которую мы указали
                doc.Create.PlaceGroup(pointOfCentrRoomIn, group.GroupType);
                transaction.Commit();
            }
            // теперь нужно определить один или несколько блоков catch, кот-е будут обрабатывать исключения
            // можно сделать их два, но сделаем отдельно: 1 - с нажатием escape, 2 - все остальные ошибки, кот-е могли произойти в процессе работы
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // прекращаем работу плагина, возвращая результат отмены
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                // при этом, чтобы пользователь понимал, что произошло, в параметр message передаём результат ошибки
                message = ex.Message;
                // прекращаем работу плагина, возвращая результат провала
                return Result.Failed;
            }
            return Result.Succeeded;

        }

        // метод должен определять комнату, т.е. возвращать Room по исходной точке
        public Room GetRoomByPoint(Document doc, XYZ point)
        {
            // для поиска необходимо создать экземпляр класса
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            // поиск на основе выбранного фильтра
            collector.OfCategory(BuiltInCategory.OST_Rooms);
            // переберём всё содержимое фильтра
            foreach (Element el in collector)
            {
                // возьмём объект и попытаемся привести его к типу комнаты
                Room room = el as Room;
                if (room != null)
                {
                    // содержит ли комната точку 
                    if (room.IsPointInRoom(point))
                    {
                        return room;
                    }
                }
            }
            // если не найдём комнату, котрой принадлежит точка
            return null;
        }

        public XYZ GetElementCenter(Element element)
        {
            // get_BoundingBox принимает аргумент вид или пустую ссылку
            BoundingBoxXYZ boundingBoxXYZ = element.get_BoundingBox(null);
            // BoundingBoxXYZ -рамка в 3-х измерениях, т.е. параллелепипед
            // центр найти - среднее между максим и миним точкой BoundingBox
            return (boundingBoxXYZ.Max + boundingBoxXYZ.Min) / 2;
        }

        // создадим класс ISelectionFilter для фильтра 
        public class GroupPickFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                // возвращаем true/false в зависимости от типа элемента: если элемент явл-ся группой нужно вернуть true; если явл-ся чем-то другим вернуть false
                // если элемент, над кот-м находится курсор имеет категорию OST_IOSModelGroups
                if (elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_IOSModelGroups)
                    return true;
                else
                    return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                // так как нас интересует только элемент
                return false;
            }
        }
    }
}
