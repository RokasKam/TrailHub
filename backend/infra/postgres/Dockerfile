# postgis/postgis has no arm64 image, so add PostGIS to the official multi-arch Postgres image.
FROM postgres:17-bookworm
# Upgrade the server together with PostGIS: the PostgreSQL apt repository builds PostGIS against its newest 17.x
# point release, which can be newer than the one in this image (e.g. PostGIS built for 17.12 fails to load on 17.11
# with "undefined symbol: heap_datum_check"). Point releases keep the same data format, so this is safe.
RUN apt-get update \
 && apt-get install -y --no-install-recommends postgresql-17 postgresql-17-postgis-3 \
 && rm -rf /var/lib/apt/lists/*
