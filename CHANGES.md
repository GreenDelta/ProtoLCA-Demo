# Changelog

## 2022-08-23

### Changed

* updated library and gRPC tools version
* updated the proto-files:
  * commons.proto:
    * added `epd` and `result` to `ProtoDataSet`; removed `category`
    * renamed `ProtoTechFlow.process` to `ProtoTechFlow.provider` (as also
      sub-systems and results can be a provider of a product or waste flow
      in a product system)
  * result_service.proto:
    * added `ProtoCalculationSetup` as this was removed from the openLCA
      schema; it now has choice for `process` (direct process calculation) or
      `product_system` (product system calculation)
  * olca.proto:
    * added updates of the latest [openLCA schema](https://github.com/GreenDelta/olca-schema)
    * added `Epd` and `Result` types
    * categories are just string paths now
    * new (stable) field indices
    * use optional annotations for some primitive fields 
    * ... see the olca-schema repo for a full change log