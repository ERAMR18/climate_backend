FROM maven:3.9-eclipse-temurin-21 AS build
WORKDIR /app
COPY pom.xml ./
COPY src ./src
RUN mvn -B -ntp verify
FROM eclipse-temurin:21-jre-alpine
RUN addgroup -S climate && adduser -S climate -G climate
WORKDIR /app
COPY --from=build /app/target/alert-service-3.0.0.jar ./alert-service.jar
USER climate
EXPOSE 8080
ENTRYPOINT ["java", "-XX:MaxRAMPercentage=75.0", "-jar", "alert-service.jar"]
