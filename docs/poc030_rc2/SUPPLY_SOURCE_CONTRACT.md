# Supply remains Unknown

Positive recipient-specific native service operations are required. Proximity to a supply object, animation state, stock changes or a resupply order do not prove successful service. SupplySystem has private operation methods and ResupplyComponent has accumulated fields, but their exact successful-operation semantics and recipient lifetime are not yet validated.

Runtime canonical input consequently sets SupplyComplete=false. No synthetic service events are injected. The observer refuses unvalidated ServiceEvent messages. Existing autonomous interval tests and StreamingRelief do not demonstrate a live provider. Implementing and validating a passive native operation feed remains required before this part of TZ17 can close.
