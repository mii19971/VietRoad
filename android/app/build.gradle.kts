plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.serialization")
}

android {
    namespace = "com.vietroad.app"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.vietroad.app"
        minSdk = 21          // Android 5.0: chay duoc tren ca dau Android o to doi cu
        targetSdk = 34
        versionCode = 1
        versionName = "0.1.0"

        // BuildConfig.API_BASE_URL: doc tu gradle.properties, code Kotlin dung duoc.
        val apiBaseUrl = (project.findProperty("vietroad.apiBaseUrl") as String?) ?: "http://10.0.2.2:5000/"
        buildConfigField("String", "API_BASE_URL", "\"$apiBaseUrl\"")

        // Mac dinh (ban release) CHAN http khong ma hoa.
        manifestPlaceholders["usesCleartextTraffic"] = "false"
    }

    buildTypes {
        debug {
            // Ban debug cho phep http:// de goi API tren may tinh khi dang phat trien.
            manifestPlaceholders["usesCleartextTraffic"] = "true"
        }
        release {
            isMinifyEnabled = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true      // giao dien viet bang Jetpack Compose
        buildConfig = true  // sinh class BuildConfig
    }
    composeOptions {
        // Phien ban trinh bien dich Compose phai khop voi Kotlin 1.9.24.
        kotlinCompilerExtensionVersion = "1.5.14"
    }
    packaging {
        resources.excludes += "/META-INF/{AL2.0,LGPL2.1}"
    }
}

dependencies {
    // Compose: BOM chon san cac phien ban tuong thich voi nhau.
    implementation(platform("androidx.compose:compose-bom:2024.06.00"))
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.activity:activity-compose:1.9.0")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.2")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.8.2")
    implementation("androidx.core:core-ktx:1.13.1")

    // Goi API: OkHttp gui HTTP, kotlinx.serialization doi JSON <-> class Kotlin.
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.6.3")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")

    testImplementation("junit:junit:4.13.2")
}
