# postgis/postgis has no arm64 image, so add PostGIS to the official multi-arch Postgres image.
FROM postgres:17-bookworm
RUN apt-get update \
 && apt-get install -y --no-install-recommends postgresql-17-postgis-3 \
 && rm -rf /var/lib/apt/lists/*
