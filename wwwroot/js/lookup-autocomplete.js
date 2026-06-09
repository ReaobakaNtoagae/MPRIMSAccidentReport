var LookupAutoComplete = (function ($) {
    "use strict";

    function create(opts) {
        var endpoint = opts.endpoint;
        var onSelected = opts.onValueSelected || function () { };
        var container = $(opts.container);
        var lastItems = [];

        var inst = container.dxAutocomplete({
            placeholder: opts.placeholder || "Search or type to add...",
            showClearButton: true,
            minSearchLength: 1,
            valueExpr: "text",
            displayExpr: "text",

            dataSource: {
                load: function (loadOptions) {
                    var q = loadOptions.searchValue || "";
                    if (!q) return [];

                    return $.getJSON(
                        "/api/lookup/" + endpoint + "?q=" + encodeURIComponent(q)
                    ).then(function (items) {
                        lastItems = items || [];

                        var exact = lastItems.some(function (i) {
                            return i.text.toUpperCase() === q.toUpperCase();
                        });

                        if (!exact && q.trim()) {
                            lastItems = lastItems.concat([{
                                text: q.trim(),
                                _isNew: true,
                                _label: '+ Add "' + q.trim() + '"'
                            }]);
                        }
                        return lastItems;
                    });
                },
                byKey: function (key) {
                    var found = lastItems.find(function (i) { return i.text === key; });
                    return found
                        ? $.Deferred().resolve(found).promise()
                        : $.Deferred().resolve({ text: key }).promise();
                }
            },

            itemTemplate: function (data) {
                if (data._isNew) {
                    return $("<div>").css({
                        color: "#2e7d32", fontWeight: "700", fontStyle: "italic"
                    }).text(data._label);
                }
                var label = data.text;
                if (data.description) label += " — " + data.description;
                if (data.district) label += " (" + data.district + ")";
                return $("<div>").text(label);
            },

            onSelectionChanged: function (e) {
                var sel = e.selectedItem;
                if (!sel) return;

                if (sel._isNew) {
                    $.ajax({
                        url: "/api/lookup/" + endpoint,
                        method: "POST",
                        contentType: "application/json",
                        data: JSON.stringify({ text: sel.text }),
                        success: function (created) {
                            DevExpress.ui.notify(
                                '"' + created.text + '" added.', "success", 3000);
                            inst.option("value", created.text);
                            onSelected(created);
                        },
                        error: function (xhr) {
                            
                            onSelected({ text: sel.text });
                        }
                    });
                } else {
                    onSelected(sel);
                }
            }
        }).dxAutocomplete("instance");

        return inst;
    }

    return { create: create };

}(jQuery));